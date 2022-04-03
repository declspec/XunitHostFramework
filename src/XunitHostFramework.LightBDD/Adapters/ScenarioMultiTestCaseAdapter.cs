using XunitHostFramework;
using XunitHostFramework.Adapters;
using XunitHostFramework.Runners;
using LightBDD.XUnit2;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.LightBDD.Adapters
{
    public class ScenarioMultiTestCaseAdapter : IXunitTestCaseAdapter
    {
        private static readonly Type ScenarioMultiTestCaseType = typeof(ScenarioAttribute)
            .Assembly.GetType("LightBDD.XUnit2.Implementation.Customization.ScenarioMultiTestCase");

        public bool CanHandle(IXunitTestCase test)
        {
            if (ScenarioMultiTestCaseType == null)
                throw new InvalidOperationException($"Unable to find LightBDD class 'ScenarioMultiTestCase'");

            return ScenarioMultiTestCaseType.IsAssignableFrom(test.GetType());
        }

        public Task<RunSummary> RunAsync(IXunitTestCase test, IXunitHost host, IServiceProvider services, IMessageSink diagnosticMessageSink, IMessageBus messageBus, DeferredConstructorArguments constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            return new ScenarioMultiTestCaseRunner(test, host, services, test.DisplayName, test.SkipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource)
                .RunAsync();
        }

        private class ScenarioMultiTestCaseRunner : XunitHostTheoryTestCaseRunner
        {
            private static readonly Type ScenarioTestRunnerType = typeof(ScenarioAttribute)
                .Assembly.GetType("LightBDD.XUnit2.Implementation.Customization.ScenarioTestRunner");

            private static readonly MethodInfo RunScenarioAsyncMethod = GetRunScenarioAsyncMethod();
            private static readonly ConstructorInfo ScenarioTestRunnerConstructor = GetScenarioTestRunnerConstructor();

            public ScenarioMultiTestCaseRunner(IXunitTestCase testCase, IXunitHost host, IServiceProvider services, string displayName, string skipReason, DeferredConstructorArguments constructorArguments, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
                : base(testCase, host, services, displayName, skipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource)
            { }

            protected override XunitTestRunner CreateTestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, string skipReason, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
                => CreateScenarioTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource);

            protected override Task<RunSummary> ExecuteRunnerAsync(XunitTestRunner runner)
                => RunScenarioAsync(runner);

            private Task<RunSummary> RunScenarioAsync(XunitTestRunner runner)
            {
                if (RunScenarioAsyncMethod == null)
                    throw new InvalidOperationException($"Unable to find LightBDD ScenarioTestRunner.{nameof(RunScenarioAsync)} method");

                return (Task<RunSummary>)RunScenarioAsyncMethod.Invoke(runner, null);
            }

            private static XunitTestRunner CreateScenarioTestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments, MethodInfo testMethod, object[] testMethodArguments, string skipReason, IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                if (ScenarioTestRunnerConstructor == null)
                    throw new InvalidOperationException("Unable to find LightBDD ScenarioTestRunner constructor");

                return (XunitTestRunner)ScenarioTestRunnerConstructor.Invoke(new object[] { test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource });
            }

            private static MethodInfo GetRunScenarioAsyncMethod()
            {
                if (ScenarioTestRunnerType == null)
                    return null;

                var method = ScenarioTestRunnerType.GetMethod(nameof(RunScenarioAsync), BindingFlags.Public | BindingFlags.Instance);

                return typeof(Task<RunSummary>).IsAssignableFrom(method.ReturnType)
                    ? method
                    : null;
            }

            private static ConstructorInfo GetScenarioTestRunnerConstructor()
            {
                if (ScenarioTestRunnerType == null)
                    return null;

                // Mirror the parameter types from the CreateScenarioTestRunner method
                var parameters = typeof(ScenarioMultiTestCaseRunner)
                    .GetMethod(nameof(CreateScenarioTestRunner), BindingFlags.Static | BindingFlags.NonPublic)
                    .GetParameters();

                return ScenarioTestRunnerType.GetConstructor(Array.ConvertAll(parameters, p => p.ParameterType));
            }
        }
    }
}
