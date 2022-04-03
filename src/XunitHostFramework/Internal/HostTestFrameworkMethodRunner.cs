using XunitHostFramework.Adapters;
using XunitHostFramework.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public class HostTestFrameworkMethodRunner : TestMethodRunner<IXunitTestCase>
    {
        protected IXunitHost Host { get; }
        protected IServiceScope Scope { get; }
        protected IMessageSink DiagnosticMessageSink { get; }
        protected DeferredConstructorArguments ConstructorArguments { get; }

        public HostTestFrameworkMethodRunner(IXunitHost host, IServiceProvider services, ITestMethod testMethod, IReflectionTypeInfo @class, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, DeferredConstructorArguments constructorArguments) 
            : base(testMethod, @class, method, testCases, messageBus, aggregator, cancellationTokenSource)
        {
            Host = host;
            Scope = services.CreateLifetimeScope(LifetimeScopes.Method);
            DiagnosticMessageSink = diagnosticMessageSink;
            ConstructorArguments = constructorArguments;
        }

        protected override Task<RunSummary> RunTestCasesAsync()
        {
            // Allow parallelisation at the method-level (i.e. Theories with multiple test cases)
            var testTasks = TestCases.Select(c => (Func<Task<RunSummary>>)(() => RunTestCaseAsync(c)))
                .ToList();

            var options = Scope.ServiceProvider.GetService<IOptionsSnapshot<XunitHostOptions>>();
            var allowParallelExecution = options?.Value?.AllowTestParallelization ?? true;

            return TestExecutor.RunAsync(TestMethod.TestClass, testTasks, allowParallelExecution, CancellationTokenSource.Token);
        }

        protected override Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
        {
            if (testCase is ExecutionErrorTestCase)
                return testCase.RunAsync(DiagnosticMessageSink, MessageBus, Array.Empty<object>(), new ExceptionAggregator(Aggregator), CancellationTokenSource);

            // NOTE: An XunitTheoryTestCase will actually run multiple internal 'test cases' that weren't discovered by the Framework.
            //  This only happens when the data rows are discovered at runtime; because are either
            //    a) not-discoverable (randomized datasets, [MemberData] that refers to a runtime function, custom discoverers, etc.)
            //    b) not-serializable (i.e complex objects that don't implement ISerializable)
            //  This limits dependency injection and test parallelization as the Framework does not provide any mechanism to discover these nested tests.
            //  By default this means that "transient" dependencies will be shared with all the nested test cases, and none of these nested test cases will be parallelised.
            //
            // To help with this, IXunitHostTestCaseAdapters can be registered to unpack these tests and execute them properly.
            //  see: Adapters.XunitTheoryTestCaseAdapter for an example that wraps the default XunitTheoryTestCaseRunner

            var adapter = Scope.ServiceProvider.GetServices<IXunitTestCaseAdapter>()
                .Reverse()
                .FirstOrDefault(a => a.CanHandle(testCase));

            return adapter != null
                ? adapter.RunAsync(testCase, Host, Scope.ServiceProvider, DiagnosticMessageSink, MessageBus, ConstructorArguments, new ExceptionAggregator(Aggregator), CancellationTokenSource)
                : RunUnadaptedTestCaseAsync(testCase);
        }

        private async Task<RunSummary> RunUnadaptedTestCaseAsync(IXunitTestCase testCase)
        {
            // Default to resolving the constructor arguments at this point and run the test case.
            using (var testCaseScope = Scope.ServiceProvider.CreateLifetimeScope(LifetimeScopes.TestCase))
            {
                var resolvedArguments = ConstructorArguments.Resolve(testCaseScope.ServiceProvider, Aggregator);
                var summary = await testCase.RunAsync(DiagnosticMessageSink, MessageBus, resolvedArguments, new ExceptionAggregator(Aggregator), CancellationTokenSource)
                    .ConfigureAwait(false);

                return summary;
            }
        }
    }
}
