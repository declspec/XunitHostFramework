using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace XunitHostFramework
{
    public interface IXunitHostBuilder
    {
        IConfigurationBuilder Configuration { get; }
        IXunitHostBuilder ConfigureServices(Action<IServiceCollection> configure);
        IXunitHostBuilder ConfigureServices(Action<IXunitHostBuilderContext, IServiceCollection> configure);
        IXunitHostBuilder Use<TMiddleware>();
        IXunitHostBuilder Use(Func<IXunitHost, ExecutionDelegate, Task> middleware);
        IXunitHost Build();
    }
}
