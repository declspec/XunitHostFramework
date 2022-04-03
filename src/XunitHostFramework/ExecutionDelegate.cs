using System.Threading.Tasks;

namespace XunitHostFramework
{
    /// <summary>
    /// Middleware function to be called when processing the Host lifecycle
    /// </summary>
    /// <param name="host">The current host</param>
    /// <returns>A <see cref="Task"/> that completes after the middleware has completed</returns>
    public delegate Task ExecutionDelegate(IXunitHost host);
}
