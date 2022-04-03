using System;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public class HostTestFrameworkTypeDiscoverer : ITestFrameworkTypeDiscoverer
    {
        public Type GetTestFrameworkType(IAttributeInfo attribute)
        {
            // injection point for the rest of the framework
            return typeof(HostTestFramework);
        }
    }
}
