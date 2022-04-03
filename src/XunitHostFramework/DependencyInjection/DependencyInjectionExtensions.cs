using Microsoft.Extensions.DependencyInjection;
using System;

namespace XunitHostFramework.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceScope CreateLifetimeScope(this IServiceProvider provider, string lifetimeName)
        {
            LifetimeScope parentLifetime;

            try
            {
                parentLifetime = provider.GetService<LifetimeScope>();
            }
            catch (InvalidOperationException) // If `provider` is the root provider, resolving LifetimeScope will throw
            {
                parentLifetime = null;
            }

            var scope = provider.CreateScope();
            var lifetime = scope.ServiceProvider.GetRequiredService<LifetimeScope>();
      
            lifetime.Initialise(lifetimeName, parentLifetime, scope.ServiceProvider);
            return scope;
        }

        public static IServiceCollection AddLifetimeScoped<TService>(this IServiceCollection services, string lifetimeName) where TService : class
        {
            return AddLifetimeScoped(services, lifetimeName, typeof(TService));
        }

        public static IServiceCollection AddLifetimeScoped<TService, TImplementation>(this IServiceCollection services, string lifetimeName)
            where TService : class
            where TImplementation : class, TService
        {
            return AddLifetimeScoped(services, lifetimeName, typeof(TService), typeof(TImplementation));
        }

        public static IServiceCollection AddLifetimeScoped(this IServiceCollection services, string lifetimeName, Type serviceType)
        {
            return AddLifetimeScoped(services, lifetimeName, serviceType, serviceType);
        }

        public static IServiceCollection AddLifetimeScoped(this IServiceCollection services, string lifetimeName, Type serviceType, Type implementationType)
        {
            return AddLifetimeScoped(services, lifetimeName, serviceType, ctx => ActivatorUtilities.CreateInstance(ctx, implementationType));
        }

        public static IServiceCollection AddLifetimeScoped<TService>(this IServiceCollection services, string lifetimeName, Func<IServiceProvider, TService> implementationFactory) where TService : class
        {
            return AddLifetimeScoped(services, lifetimeName, typeof(TService), implementationFactory);
        }

        public static IServiceCollection AddLifetimeScoped<TService, TImplementation>(this IServiceCollection services, string lifetimeName, Func<IServiceProvider, TImplementation> implementationFactory)
            where TService : class
            where TImplementation : class, TService
        {
            return AddLifetimeScoped(services, lifetimeName, typeof(TService), implementationFactory);
        }

        public static IServiceCollection AddLifetimeScoped(this IServiceCollection services, string lifetimeName, Type serviceType, Func<IServiceProvider, object> implementationFactory)
        {
            if (string.IsNullOrEmpty(lifetimeName))
                throw new ArgumentNullException(nameof(lifetimeName));

            if (implementationFactory == null)
                throw new ArgumentNullException(nameof(implementationFactory));

            return services.AddScoped(serviceType, ctx =>
            {
                var startLifetime = ctx.GetRequiredService<LifetimeScope>();
                var lifetime = startLifetime;

                while (lifetime != null && !string.Equals(lifetime.Name, lifetimeName))
                    lifetime = lifetime.Parent;

                if (lifetime == null)
                    throw new InvalidOperationException($"Unable to resolve lifetime '{lifetimeName}' from initial lifetime '{startLifetime.Name}'");

                return lifetime == startLifetime
                    ? implementationFactory(lifetime.ServiceProvider) // If the calling scope owns the lifetime, call the real factory
                    : lifetime.ServiceProvider.GetRequiredService(serviceType); // Defer the ownership to the parent lifetime
            });
        }
    }
}
