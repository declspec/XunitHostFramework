using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace XunitHostFramework
{
    public interface IXunitHost : IDisposable
    {
        IConfiguration Configuration { get; }
        IServiceProvider Services { get; }
        XunitHostOptions Options { get; }

        Task RunAsync(ExecutionDelegate final);
    }
}
