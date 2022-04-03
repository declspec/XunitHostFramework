using Microsoft.Extensions.Configuration;

namespace XunitHostFramework
{
    public interface IXunitHostBuilderContext
    {
        IConfiguration Configuration { get; }
    }
}
