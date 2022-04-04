using XunitHostFramework.DependencyInjection;
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

namespace XunitHostFramework.Internal
{
    public class HostTestFrameworkClassRunner : XunitTestClassRunner
    {
        protected IXunitHost Host { get; }
        protected IServiceScope Scope { get; }

        public HostTestFrameworkClassRunner(IXunitHost host, IServiceProvider services, ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings) 
            : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
        {
            Host = host;
            Scope = services.CreateLifetimeScope(LifetimeScopes.Class);
        }

        /// <inheritdoc />
        protected override Task<RunSummary> RunTestMethodsAsync()
        {
            // Override the default method to change how tests can be parallelized,
            //  if XunitHostOptions.AllowTestParallelization is true then class and method
            //  level parallelization is available.
            IEnumerable<IXunitTestCase> orderedTestCases;

            try
            {
                orderedTestCases = TestCaseOrderer.OrderTestCases(TestCases);
            }
            catch (Exception ex)
            {
                var innerEx = ex.Unwrap();
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"Test case orderer '{TestCaseOrderer.GetType().FullName}' threw '{innerEx.GetType().FullName}' during ordering: {innerEx.Message}{Environment.NewLine}{innerEx.StackTrace}"));
                orderedTestCases = TestCases.ToList();
            }

            var constructorArguments = CreateDeferredConstructorArguments();

            // Default Xunit grouping to group methods by their class.
            var tasks = orderedTestCases
                .GroupBy(tc => tc.TestMethod, TestMethodComparer.Instance)
                .Select(method => (Func<Task<RunSummary>>)(() => RunTestMethodAsync(method.Key, (IReflectionMethodInfo)method.Key.Method, method, constructorArguments)))
                .ToList();

            // Pull the options from the service provider to check for parallelization
            var options = Host.Services.GetService<IOptions<XunitHostOptions>>();
            var allowParallelExecution = options?.Value?.AllowTestParallelization ?? true;

            // Use the custom executor to handle the parallel execution
            return TestExecutor.RunAsync(TestClass, tasks, allowParallelExecution, CancellationTokenSource.Token);
        }

        protected virtual DeferredConstructorArguments CreateDeferredConstructorArguments()
        {
            ConstructorInfo ctor = null;

            if ((Class.Type.IsAbstract && Class.Type.IsSealed) || ((ctor = SelectTestClassConstructor()) == null))
                return DeferredConstructorArguments.Empty;

            var parameters = ctor.GetParameters();
            var resolvedArguments = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; ++i)
            {
                if (TryGetConstructorArgument(ctor, i, parameters[i], out var resolvedArgument))
                    resolvedArguments[i] = resolvedArgument;
            }

            return new DeferredConstructorArguments(parameters, resolvedArguments, unusedArgs => FormatConstructorArgsMissingMessage(ctor, unusedArgs));
        }

        protected override bool TryGetConstructorArgument(ConstructorInfo constructor, int index, ParameterInfo parameter, out object argumentValue)
        {
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                argumentValue = CancellationTokenSource.Token;
                return true;
            }

            argumentValue = default;

            return !typeof(ITestOutputHelper).IsAssignableFrom(parameter.ParameterType)
                && base.TryGetConstructorArgument(constructor, index, parameter, out argumentValue);
        }

        protected virtual Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, DeferredConstructorArguments constructorArguments)
        {
            // Return the method-level runner
            return new HostTestFrameworkMethodRunner(Host, Scope.ServiceProvider, testMethod, Class, method, testCases, DiagnosticMessageSink, MessageBus, new ExceptionAggregator(Aggregator), CancellationTokenSource, constructorArguments).RunAsync();
        }

        protected override async Task BeforeTestClassFinishedAsync()
        {
            await base.BeforeTestClassFinishedAsync().ConfigureAwait(false);
            Aggregator.Run(Scope.Dispose);
        }
    }
}
