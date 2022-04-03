using XunitHostFramework.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public class HostTestFrameworkCollectionRunner : XunitTestCollectionRunner
    {
        protected IXunitHost Host { get; }
        protected IServiceScope Scope { get; }

        public HostTestFrameworkCollectionRunner(IXunitHost host, IServiceProvider services, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource) 
            : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
        {
            Host = host;
            Scope = services.CreateLifetimeScope(LifetimeScopes.Collection);
        }

        protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
        {
            // Replace the default class runner 
            return new HostTestFrameworkClassRunner(Host, Scope.ServiceProvider, testClass, @class, testCases, DiagnosticMessageSink, MessageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), CancellationTokenSource, CollectionFixtureMappings)
                .RunAsync();
        }

        protected override async Task BeforeTestCollectionFinishedAsync()
        {
            await base.BeforeTestCollectionFinishedAsync();
            Aggregator.Run(Scope.Dispose);
        }
    }
}
