using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Adapters
{
    public interface IXunitTestCaseAdapter
    {
        bool CanHandle(IXunitTestCase test);
        Task<RunSummary> RunAsync(IXunitTestCase test, IXunitHost host, IServiceProvider services, IMessageSink diagnosticMessageSink, IMessageBus messageBus, DeferredConstructorArguments constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource);
    }
}
