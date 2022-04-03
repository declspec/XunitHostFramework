using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public class HostTestFramework : XunitTestFramework
    {
        public HostTestFramework(IMessageSink messageSink) 
            : base(messageSink)
        { }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
            => new HostTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
    }
}
