using XunitHostFramework.Runners;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Adapters
{
    public class XunitTheoryTestCaseAdapter : IXunitTestCaseAdapter
    {
        public bool CanHandle(IXunitTestCase test)
        {
            return test is XunitTheoryTestCase;
        }

        public Task<RunSummary> RunAsync(IXunitTestCase test, IXunitHost host, IServiceProvider services, IMessageSink diagnosticMessageSink, IMessageBus messageBus, DeferredConstructorArguments constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            return new XunitHostTheoryTestCaseRunner(test, host, services, test.DisplayName, test.SkipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource)
                .RunAsync();
        }
    }
}
