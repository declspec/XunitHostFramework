using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace XunitHostFramework.Internal
{
    public class XunitHostBuilder : IXunitHostBuilder
    {
        private readonly List<Action<IXunitHostBuilderContext, IServiceCollection>> _serviceConfigurations = new List<Action<IXunitHostBuilderContext, IServiceCollection>>();
        private readonly List<IExecutionMiddleware> _middleware = new List<IExecutionMiddleware>();

        public IConfigurationBuilder Configuration { get; }

        public XunitHostBuilder(IConfigurationBuilder configuration)
        {
            Configuration = configuration;
        }

        public IXunitHostBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            _serviceConfigurations.Add((_, services) => configure(services));
            return this;
        }

        public IXunitHostBuilder ConfigureServices(Action<IXunitHostBuilderContext, IServiceCollection> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            _serviceConfigurations.Add(configure);
            return this;
        }

        public IXunitHostBuilder Use<TMiddleware>()
        {
            var middlewareType = typeof(TMiddleware);
            _middleware.Add(new TypedExecutionMiddleware(middlewareType));
            return this;
        }

        public IXunitHostBuilder Use(Func<IXunitHost, ExecutionDelegate, Task> middleware)
        {
            if (middleware == null)
                throw new ArgumentNullException(nameof(middleware));

            _middleware.Add(new AnonymousExecutionMiddleware(middleware));
            return this;
        }

        public IXunitHost Build()
        {
            var configuration = Configuration.Build();
            var context = new XunitHostBuilderContext(configuration);
            var services = new ServiceCollection();

            foreach (var configureServices in _serviceConfigurations)
                configureServices(context, services);

            var provider = services.BuildServiceProvider(true);
            return new XunitHost(configuration, provider, _middleware);
        }

        // TODO: Potentially break these implementation details out into their own files
        private class XunitHostBuilderContext : IXunitHostBuilderContext
        {
            public IConfiguration Configuration { get; }

            public XunitHostBuilderContext(IConfiguration configuration)
            {
                Configuration = configuration;
            }
        }

        private class XunitHost : IXunitHost
        {
            public IConfiguration Configuration { get; }
            public IServiceProvider Services => _services;
            public XunitHostOptions Options { get; }

            private readonly ServiceProvider _services;
            private readonly IReadOnlyList<IExecutionMiddleware> _middleware;

            public XunitHost(IConfiguration configuration, ServiceProvider services, IReadOnlyList<IExecutionMiddleware> middleware)
            {
                _services = services;
                _middleware = middleware;

                Configuration = configuration;
                Options = services.GetRequiredService<IOptions<XunitHostOptions>>().Value;
            }

            public Task RunAsync(ExecutionDelegate final)
            {
                var pipeline = _middleware.Reverse()
                    .Aggregate(final, (next, middleware) => middleware.CreateDelegate(next));

                return pipeline(this);
            }

            public void Dispose()
            {
                _services.Dispose();
            }
        }

        // Basic middleware implementations
        private interface IExecutionMiddleware
        {
            ExecutionDelegate CreateDelegate(ExecutionDelegate next);
        }

        /// <summary>
        /// Support simple anonymous function based middleware
        /// </summary>
        private class AnonymousExecutionMiddleware : IExecutionMiddleware
        {
            private readonly Func<IXunitHost, ExecutionDelegate, Task> _middleware;

            public AnonymousExecutionMiddleware(Func<IXunitHost, ExecutionDelegate, Task> middleware)
            {
                _middleware = middleware;
            }

            public ExecutionDelegate CreateDelegate(ExecutionDelegate next)
            {
                return host => _middleware(host, next);
            }
        }

        /// <summary>
        /// Support class-based middleware
        /// </summary>
        private class TypedExecutionMiddleware : IExecutionMiddleware
        {
            private const string InvokeMethodName = "InvokeAsync";

            private readonly ConstructorInfo _constructor;
            private readonly MethodInfo _invokeMethod;
            private readonly IReadOnlyList<Type> _invokeParameterTypes;

            public TypedExecutionMiddleware(Type middlewareType)
            {
                var ctor = middlewareType.GetConstructor(new Type[] { typeof(ExecutionDelegate) })
                    ?? throw new ArgumentException($"{middlewareType.FullName} does not contain a constructor with a single '{nameof(ExecutionDelegate)}' parameter");

                var invokeMethod = middlewareType.GetMethod(InvokeMethodName, BindingFlags.Public | BindingFlags.Instance);
                var parameterTypes = invokeMethod?.GetParameters().Select(p => p.ParameterType).ToList();

                if (invokeMethod == null || parameterTypes.Count == 0 || parameterTypes[0] != typeof(IXunitHost))
                    throw new ArgumentException($"{middlewareType.FullName} does not contain a public '{InvokeMethodName}' method with a first parameter '{nameof(IXunitHost)}'");

                if (invokeMethod.ReturnType != typeof(Task))
                    throw new ArgumentException($"{middlewareType.FullName} must return a '{nameof(Task)}'");

                _constructor = ctor;
                _invokeMethod = invokeMethod;
                _invokeParameterTypes = parameterTypes;
            }

            public ExecutionDelegate CreateDelegate(ExecutionDelegate next)
            {
                var instance = _constructor.Invoke(new object[] { next });

                return host =>
                {
                    var parameters = new object[_invokeParameterTypes.Count];
                    parameters[0] = host;

                    for (var i = 1; i < parameters.Length; ++i)
                        parameters[i] = host.Services.GetRequiredService(_invokeParameterTypes[i]);

                    return (Task)_invokeMethod.Invoke(instance, parameters);
                };
            }
        }
    }
}
