using System;

namespace XunitHostFramework
{
    /// <summary>
    /// Assembly-level attribute to add a Startup type registration to the XunitHostBuilder. Can be specified multiple times.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class XunitHostStartupAttribute : Attribute
    {
        public Type StartupType { get; }

        public XunitHostStartupAttribute(Type startupType)
        {
            StartupType = startupType;
        }
    }
}
