using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public class HostTestFrameworkAssemblyRunner : XunitTestAssemblyRunner
    {
        protected IXunitHost Host { get; }

        public HostTestFrameworkAssemblyRunner(IXunitHost host, ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions) 
            : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        {
            Host = host;
        }

        protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
        {
            // Replace the default collection runner 
            return new HostTestFrameworkCollectionRunner(Host, Host.Services, testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource)
                .RunAsync();
        }
    }
}
