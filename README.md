## XunitHostFramework
xUnit `TestFramework` implementation to allow dependency injection and better test parallelization to overcome some of the limitations in xUnit.

This implementation was heaving influenced by the following Open Source libaries:
- [Xunit.DependencyInjection](https://github.com/pengweiqhca/Xunit.DependencyInjection) - Dependency Injection
- [LightBDD](https://github.com/LightBDD/LightBDD) - Test Parallelization

## Getting started
An assembly-level attribute can be added to your test project to instruct xUnit to load the XunitHostFramework instead of the default that is bundled with xUnit:

*XunitHostMetadata.cs*
```cs
using namespace XunitHostFramework
[assembly: XunitHost] // load the framework
```

In addition to the core `XunitHost` attribute, one or more `XunitHostStartup` attributes can also be specified to configure the `IXunitHost` as well as configure the Dependency Injection container:

*XunitHostMetadata.cs*
```cs
using namespace XunitHostFramework
[assembly: XunitHost] // load the framework
[assembly: XunitHostStartup(typeof(CustomStartupClass))] // 
```

Each attribute should specify the `Type` of a *Startup* class.

## Defining a *Startup* class
A *Startup* class can define two methods to configure the `IXunitHost`:

```cs
void ConfigureHost(IXunitHostBuilder builder);
void Configure(...);
```

`ConfigureHost` is used to configure the `IXunitHost` before it is built: configuration, middleware and dependencies can all be setup in this method.

`Configure` is called after the `IXunitHost` has been built, any parameters specified by the function will be resolved using the newly created Dependency Injection container. This can be used to run any final configuration before the tests are run.

Both of the above methods are optional on the *Startup* class, and can be either static or instance methods.

### Example Startup:

*CustomStartupClass.cs*
```cs
public class CustomStartupClass 
{
    public void ConfigureHost(IXunitHostBuilder builder) 
    {
        // Add additional configuration
        builder.Configuration.AddJsonFile("testsettings.json");

        // Configure the DependencyInjection container
        builder.ConfigureServices(ConfigureServices);

        // Add a middleware function
        builder.Use(async (host, next) => 
        {
            Console.WriteLine("before tests run");
            await next(host).ConfigureAwait(false);
            Console.WriteLine("after tests run");
        });
    }

    private void ConfigureServices(IHostBuilderContext context, IServiceCollection services) 
    {
        services.Configure<TestSettings>(context.Configuration.Get("TestSettings"));
        services.AddSingleton(new SingletonDependency());
        services.AddScoped<ScopedDependency>(); // NOTE: A 'scoped' dependency will be shared between all tests in an xUnit "Collection", by default all methods within a test class are part of the same "Collection".
    }

    public void Configure(IMessageSink sink) 
    {
        // NOTE: IMessageSink is registered for you by the framework by default
        sink.OnMessage(new DiagnosticMessage("host configuration complete"));
    }
}
```

## Overriding default options
The framework uses the [Options pattern](https://docs.microsoft.com/en-us/dotnet/core/extensions/options) to register the [`XunitHostOptions`](./src/XunitHostOptions.cs). You can use the [PostConfigure](https://docs.microsoft.com/en-us/dotnet/core/extensions/options#options-post-configuration) method during service configuration to override any of the default values.

```cs
public class CustomStartupClass
{
    public void ConfigureHost(IXunitHostBuilder builder) 
    {
        builder.ConfigureServices(services => 
        {
            services.PostConfigure<XunitHostOptions>(options => 
            {
                options.AllowTestParallelization = false; // disable test parallelization
            });
        });
    }
}
```

## Test parallelization caveats
The following caveats will apply when attempting to parallelize tests that would otherwise not be parallelized by xUnit:

- Tests that specify a `[Collection]` attribute will not be parallelizable
- Tests that implement the `IClassFixture<>` or `ICollectionFixture<>` interfaces will also not be parallelizable

This is to maintain consistency with xUnit's current behaviour and avoid problematic race conditions with sharing Collection and Class fixtures between parallel tests.

With the addition of a proper Dependency Injection container, the need for Collection and Class fixtures in new code should virtually disappear and result in the majority of test classes being eligible for parallelization.
