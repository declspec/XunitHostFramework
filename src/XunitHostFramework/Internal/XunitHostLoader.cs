using XunitHostFramework.Adapters;
using XunitHostFramework.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace XunitHostFramework.Internal
{
    public static class XunitHostLoader
    {
        /// <summary>
        /// Create an <see cref="IXunitHost"/>, configuring it with any registered Startups
        /// </summary>
        /// <param name="startupTypes">A list of types implementing the Startup pattern to configure the host</param>
        /// <param name="diagnosticMessageSink"></param>
        /// <returns></returns>
        public static IXunitHost CreateHost(IEnumerable<Type> startupTypes, IMessageSink diagnosticMessageSink)
        {
            if (startupTypes == null)
                throw new ArgumentNullException(nameof(startupTypes));

            var startups = startupTypes.Select(t => new Startup(t))
                .ToList();

            var hostBuilder = new XunitHostBuilder(new ConfigurationBuilder());

            // Register default services first, to allow Startups to remove/tweak the registrations
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(diagnosticMessageSink);
                services.AddSingleton<IXunitTestCaseAdapter, XunitTheoryTestCaseAdapter>();
                services.AddLifetimeScoped<ITestOutputHelper, TestOutputHelper>(LifetimeScopes.TestCase);
                services.AddScoped<LifetimeScope>();

                services.AddOptions<XunitHostOptions>()
                    .Configure(ConfigureDefaultHostOptions);
            });

            foreach (var startup in startups)
                startup.ConfigureHost(hostBuilder);

            var host = hostBuilder.Build();

            foreach (var startup in startups)
                startup.Configure(host);

            return host;
        }
        
        private static void ConfigureDefaultHostOptions(XunitHostOptions options)
        {
            options.AllowTestParallelization = true;
        }

        private class Startup
        {
            private readonly MethodInfo _configureHostMethod;
            private readonly MethodInfo _configureMethod;
            private readonly object _instance;

            public Startup(Type startupType)
            {
                _configureHostMethod = FindVoidMethod(startupType, nameof(ConfigureHost));
                _configureMethod = FindVoidMethod(startupType, nameof(Configure));

                // Verify the method signature
                if (_configureHostMethod != null)
                {
                    var parameters = _configureHostMethod.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(IXunitHostBuilder))
                        throw new InvalidOperationException($"{startupType.FullName}.{_configureHostMethod.Name} must have a single '{nameof(IXunitHostBuilder)}' parameter");
                }

                // Only create an instance if required
                if (_configureHostMethod?.IsStatic == false || _configureMethod?.IsStatic == false)
                {
                    if (startupType.IsAbstract || startupType.GetConstructor(Type.EmptyTypes) == null)
                        throw new InvalidOperationException($"'{startupType.FullName}' must have a single parameterless public constructor.");

                    _instance = Activator.CreateInstance(startupType);
                }    
            }

            public void ConfigureHost(IXunitHostBuilder hostBuilder)
            {
                _configureHostMethod?.Invoke(_configureHostMethod.IsStatic ? null : _instance, new object[] { hostBuilder });
            }

            public void Configure(IXunitHost host)
            {
                if (_configureMethod != null)
                {
                    using (var scope = host.Services.CreateScope())
                    {
                        var resolvedParameters = _configureMethod.GetParameters()
                            .Select(p => scope.ServiceProvider.GetRequiredService(p.ParameterType))
                            .ToArray();

                        _configureMethod.Invoke(_configureMethod.IsStatic ? null : _instance, resolvedParameters);
                    }
                }
            }

            private static MethodInfo FindVoidMethod(Type startupType, string methodName)
            {
                var selectedMethods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(method => method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (selectedMethods.Count > 1)
                    throw new InvalidOperationException($"{startupType.FullName} contains multiple overloads of '{methodName}'");

                var methodInfo = selectedMethods.FirstOrDefault();

                if (methodInfo != null && methodInfo.ReturnType != typeof(void))
                    throw new InvalidOperationException($"The '{methodInfo.Name}' method in the type '{startupType.FullName}' must have no return type.");

                return methodInfo;
            }
        }
    }
}
