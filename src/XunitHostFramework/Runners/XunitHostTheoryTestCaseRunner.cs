using XunitHostFramework.DependencyInjection;
using XunitHostFramework.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Runners
{
    // Re-implementation of xUnit's XunitTheoryTestCaseRunner, but with 
    //  better extensibility points and DependencyInjection/Parallelization support
    public class XunitHostTheoryTestCaseRunner : XunitTestCaseRunner
    {
        private static readonly MethodInfo SerializationHelperGetTypeMethod = typeof(XunitTheoryTestCase)
            .Assembly.GetType("Xunit.Sdk.SerializationHelper")?.GetMethod("GetType", new[] { typeof(string), typeof(string) });

        protected IXunitHost Host { get; }
        protected IServiceProvider ServiceProvider { get; }
        protected IMessageSink DiagnosticMessageSink { get; }

        private readonly DeferredConstructorArguments _constructorArguments;
        private readonly ExceptionAggregator _cleanupAggregator;
        private readonly IList<XunitTestRunner> _testRunners;
        private readonly IList<IDisposable> _disposables;

        private Exception _dataDiscoveryException;

        public XunitHostTheoryTestCaseRunner(IXunitTestCase testCase, IXunitHost host, IServiceProvider services, string displayName, string skipReason, DeferredConstructorArguments constructorArguments, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            : base(testCase, displayName, skipReason, Array.Empty<object>(), Array.Empty<object>(), messageBus, aggregator, cancellationTokenSource)
        {
            Host = host;
            ServiceProvider = services;
            DiagnosticMessageSink = diagnosticMessageSink;

            _constructorArguments = constructorArguments;
            _cleanupAggregator = new ExceptionAggregator();
            _testRunners = new List<XunitTestRunner>();
            _disposables = new List<IDisposable>();
        }

        /// <inheritdoc/>
        protected override async Task AfterTestCaseStartingAsync()
        {
            await base.AfterTestCaseStartingAsync();

            try
            {
                var dataAttributes = TestCase.TestMethod.Method.GetCustomAttributes(typeof(DataAttribute));

                foreach (var dataAttribute in dataAttributes)
                {
                    var discovererAttribute = dataAttribute.GetCustomAttributes(typeof(DataDiscovererAttribute)).First();
                    var args = discovererAttribute.GetConstructorArguments().Cast<string>().ToList();
                    var discovererType = LoadType(args[1], args[0]);

                    if (discovererType == null)
                    {
                        if (dataAttribute is IReflectionAttributeInfo reflectionAttribute)
                            Aggregator.Add(new InvalidOperationException($"Data discoverer specified for {reflectionAttribute.Attribute.GetType()} on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not exist."));
                        else
                            Aggregator.Add(new InvalidOperationException($"A data discoverer specified on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not exist."));

                        continue;
                    }

                    IDataDiscoverer discoverer;
                    try
                    {
                        discoverer = ExtensibilityPointFactory.GetDataDiscoverer(DiagnosticMessageSink, discovererType);
                    }
                    catch (InvalidCastException)
                    {
                        if (dataAttribute is IReflectionAttributeInfo reflectionAttribute)
                            Aggregator.Add(new InvalidOperationException($"Data discoverer specified for {reflectionAttribute.Attribute.GetType()} on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not implement IDataDiscoverer."));
                        else
                            Aggregator.Add(new InvalidOperationException($"A data discoverer specified on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not implement IDataDiscoverer."));

                        continue;
                    }

                    var data = discoverer.GetData(dataAttribute, TestCase.TestMethod.Method);
                    if (data == null)
                    {
                        Aggregator.Add(new InvalidOperationException($"Test data returned null for {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name}. Make sure it is statically initialized before this test method is called."));
                        continue;
                    }

                    foreach (var dataRow in data)
                    {
                        foreach (var disposable in dataRow.OfType<IDisposable>())
                            _disposables.Add(disposable);

                        ITypeInfo[] resolvedTypes = null;
                        var methodToRun = TestMethod;
                        var convertedDataRow = methodToRun.ResolveMethodArguments(dataRow);

                        if (methodToRun.IsGenericMethodDefinition)
                        {
                            resolvedTypes = TestCase.TestMethod.Method.ResolveGenericTypes(convertedDataRow);
                            methodToRun = methodToRun.MakeGenericMethod(resolvedTypes.Select(t => ((IReflectionTypeInfo)t).Type).ToArray());
                        }

                        var parameterTypes = methodToRun.GetParameters().Select(p => p.ParameterType).ToArray();
                        convertedDataRow = Reflector.ConvertArguments(convertedDataRow, parameterTypes);

                        var theoryDisplayName = TestCase.TestMethod.Method.GetDisplayNameWithArguments(DisplayName, convertedDataRow, resolvedTypes);
                        var test = CreateTest(TestCase, theoryDisplayName);
                        var skipReason = SkipReason ?? dataAttribute.GetNamedArgument<string>("Skip");

                        var scope = ServiceProvider.CreateLifetimeScope(LifetimeScopes.TestCase);
                        var resolvedArguments = ResolveConstructorArguments(scope.ServiceProvider, Aggregator);

                        _disposables.Add(scope);
                        _testRunners.Add(CreateTestRunner(test, MessageBus, TestClass, resolvedArguments, methodToRun, convertedDataRow, skipReason, BeforeAfterAttributes, Aggregator, CancellationTokenSource));
                    }
                }
            }
            catch (Exception ex)
            {
                // Stash the exception so we can surface it during RunTestAsync
                _dataDiscoveryException = ex;
            }
        }

        /// <inheritdoc />
        protected override Task BeforeTestCaseFinishedAsync()
        {
            Aggregator.Aggregate(_cleanupAggregator);
            return base.BeforeTestCaseFinishedAsync();
        }

        /// <inheritdoc />
        /// <remarks>The <paramref name="constructorArguments"/> are already resolved by the host dependency injection container</remarks>
        protected override XunitTestRunner CreateTestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, string skipReason, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            return base.CreateTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource);
        }

        protected virtual Task<RunSummary> ExecuteRunnerAsync(XunitTestRunner runner)
        {
            // Default implementation, but allow derived class to implement custom behaviour
            return runner.RunAsync();
        }

        protected object[] ResolveConstructorArguments(IServiceProvider services, ExceptionAggregator aggregator)
        {
            return _constructorArguments.Resolve(services, new ExceptionAggregator(aggregator));
        }

        /// <inheritdoc />
        protected override async Task<RunSummary> RunTestAsync()
        {
            // Essentially the same as the xUnit implementation, however allowing individual runner customisation via ExecuteRunnerAsync
            //  as well as parallelization via TestExecutor
            if (TryCreateDataDiscoveryFailure(out var discoverySummary))
                return discoverySummary;

            // TODO: More portable way of getting the XunitHostOptions?
            var options = Host.Services.GetService<IOptions<XunitHostOptions>>();
            var allowParallelExecution = options?.Value?.AllowTestParallelization ?? true;

            var runFunctions = _testRunners
                .Select(r => (Func<Task<RunSummary>>)(() => ExecuteRunnerAsync(r)))
                .ToList();

            var summary = await TestExecutor.RunAsync(
                TestCase.TestMethod.TestClass,
                runFunctions,
                allowParallelExecution,
                CancellationTokenSource.Token
            );

            // Run the cleanup here so we can include cleanup time in the run summary,
            // but save any exceptions so we can surface them during the cleanup phase,
            // so they get properly reported as test case cleanup failures.
            var timer = new ExecutionTimer();
            foreach (var disposable in _disposables)
                timer.Aggregate(() => _cleanupAggregator.Run(disposable.Dispose));

            summary.Time += timer.Total;
            return summary;
        }

        // Taken from XunitTheoryTestCaseRunner.RunTest_DataDiscoveryException
        private bool TryCreateDataDiscoveryFailure(out RunSummary summary)
        {
            if (_dataDiscoveryException == null)
            {
                summary = null;
                return false;
            }

            var test = new XunitTest(TestCase, DisplayName);

            var enqueued = MessageBus.QueueMessage(new TestStarting(test))
                && MessageBus.QueueMessage(new TestFailed(test, 0, null, _dataDiscoveryException.Unwrap()))
                && MessageBus.QueueMessage(new TestFinished(test, 0, null));

            if (!enqueued)
                CancellationTokenSource.Cancel();

            summary = new RunSummary { Total = 1, Failed = 1 };
            return true;
        }

        private static Type LoadType(string assemblyName, string typeName)
        {
            // Try and use the internal xUnit SerializationHelper.GetType(...) method if it's available
            if (SerializationHelperGetTypeMethod != null)
                return (Type)SerializationHelperGetTypeMethod.Invoke(null, new[] { assemblyName, typeName });

            try
            {
                var asm = new AssemblyName(assemblyName);
                var assembly = Assembly.Load(new AssemblyName { Name = asm.Name, Version = asm.Version });
                return assembly.GetType(typeName);
            }
            catch
            {
                return null;
            }
        }
    }
}
