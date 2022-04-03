using XunitHostFramework.Adapters;
using Microsoft.Extensions.DependencyInjection;
using XunitHostFramework.LightBDD.Adapters;
using XunitHostFramework.LightBDD.Internal;

namespace XunitHostFramework.LightBDD
{
    public static class XunitHostBuilderExtensions
    {
        public static IXunitHostBuilder UseLightBdd(this IXunitHostBuilder builder)
        {
            builder.Use(async (host, next) =>
            {
                using (var scope = new LightBddScope())
                    await next(host).ConfigureAwait(false);
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IXunitTestCaseAdapter, ScenarioMultiTestCaseAdapter>();
            });

            return builder;
        }
    }
}
