using XunitHostFramework.Internal;
using System;
using Xunit.Sdk;

namespace XunitHostFramework
{
    /// <summary>
    /// Assembly level attribute to set the HostTestFramework as the XUnit framework of choice
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    [TestFrameworkDiscoverer("XunitHostFramework.Internal." + nameof(HostTestFrameworkTypeDiscoverer), "XunitHostFramework")]
    public class XunitHostAttribute : Attribute, ITestFrameworkAttribute
    {
    }
}
