namespace XunitHostFramework
{
    /// <summary>
    /// Options used by the XunitHost and HostFramework at runtime. Can be configured using <see cref="Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions.PostConfigure{TOptions}(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{TOptions})"/>
    /// </summary>
    public class XunitHostOptions
    {
        public bool AllowTestParallelization { get; set; }
    }
}
