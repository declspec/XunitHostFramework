using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public class HostTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        public HostTestFrameworkExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink) 
            : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
        { }

        protected override void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            RunTestCasesAsync(testCases, executionMessageSink, executionOptions)
                .GetAwaiter()
                .GetResult();
        }

        protected virtual async Task RunTestCasesAsync(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            // Configure the XunitHost with all registered Startup classes in the assembly
            var assembly = Assembly.Load(new AssemblyName(TestAssembly.Assembly.Name));
            var attributes = GetXunitHostStartupAttributes(assembly);

            using (var host = XunitHostLoader.CreateHost(attributes.Select(a => a.StartupType), DiagnosticMessageSink))
            {
                // Run the assembly runner in the context of the Host's lifecycle
                //  this allows any middeware to do setup/teardown
                await host.RunAsync(async _ =>
                {
                    using (var assemblyRunner = new HostTestFrameworkAssemblyRunner(host, TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions))
                        await assemblyRunner.RunAsync().ConfigureAwait(false);
                });
            }
        }

        private IReadOnlyList<XunitHostStartupAttribute> GetXunitHostStartupAttributes(Assembly assembly)
        {
            return assembly.GetCustomAttributes<XunitHostStartupAttribute>()
                .ToList();
        }
    }
}
