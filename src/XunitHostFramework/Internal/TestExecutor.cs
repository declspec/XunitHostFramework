using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    // Taken from LightBDD.XUnit2
    //  ref: https://github.com/LightBDD/LightBDD/blob/ca7ccb06fef9d1fa96583c1e8db572dab915f4d3/src/LightBDD.XUnit2/Implementation/Customization/TaskExecutor.cs
    public class TestExecutor
    {
        private static readonly Type[] FixtureTypes = { typeof(IClassFixture<>), typeof(ICollectionFixture<>) };

        private static Task<RunSummary> RunOnThreadPool(CancellationToken token, Func<Task<RunSummary>> code)
        {
            if (SynchronizationContext.Current == null)
                return Task.Run(code, token);

            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            return Task.Factory.StartNew(code, token, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler, scheduler).Unwrap();
        }

        public static async Task<RunSummary> RunAsync(ITestClass testClass, IReadOnlyList<Func<Task<RunSummary>>> tasks, bool allowParallelExecution = true, CancellationToken cancellationToken = default)
        {
            var runSummary = new RunSummary();

            if (tasks.Count > 1 && allowParallelExecution && IsParallelizable(testClass))
            {
                // Run in parallel
                var summaries = await Task.WhenAll(tasks.Select(task => RunOnThreadPool(cancellationToken, task)))
                    .ConfigureAwait(false);

                foreach (var summary in summaries)
                {
                    var slowestTime = Math.Max(summary.Time, runSummary.Time);
                    runSummary.Aggregate(summary);
                    runSummary.Time = slowestTime;
                }
            }
            else
            { 
                // Run sequentially
                foreach (var task in tasks)
                    runSummary.Aggregate(await task().ConfigureAwait(false));
            }

            return runSummary;
        }

        private static bool IsParallelizable(ITestClass testClass)
        {
            return !testClass.Class.GetCustomAttributes(typeof(CollectionAttribute)).Any()
                && testClass.Class.Interfaces.All(type => !type.IsGenericType || !FixtureTypes.Contains(type.ToRuntimeType().GetGenericTypeDefinition()));
        }
    }
}
