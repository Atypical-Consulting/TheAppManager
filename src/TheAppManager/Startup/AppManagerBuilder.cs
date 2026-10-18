using Microsoft.AspNetCore.Builder;
using TheAppManager.Modules;

namespace TheAppManager.Startup;

/// <summary>
/// Builds an <see cref="AppManager"/> by applying an <see cref="AppModuleCollection"/>
/// to a <see cref="WebApplicationBuilder"/>.
/// </summary>
public class AppManagerBuilder
{
    private readonly WebApplicationBuilder _builder;
    private readonly List<Action<WebApplicationBuilder>> _configureBuilder = [];

    /// <summary>
    /// Initializes a new instance of <see cref="AppManagerBuilder"/> with the specified command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the web application.</param>
    public AppManagerBuilder(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        _builder = WebApplication.CreateBuilder(args);
    }

    /// <summary>
    /// Registers an optional callback to configure the <see cref="WebApplicationBuilder"/>
    /// before the application is built. Callbacks accumulate and run in call order.
    /// </summary>
    /// <param name="configure">A callback that receives the builder, or <c>null</c> to skip.</param>
    /// <returns>This builder instance for chaining.</returns>
    public AppManagerBuilder ConfigureBuilder(Action<WebApplicationBuilder>? configure)
    {
        if (configure is not null)
        {
            _configureBuilder.Add(configure);
        }

        return this;
    }

    /// <summary>
    /// Builds the web application by applying all modules in order.
    /// </summary>
    /// <param name="modules">The module collection that configures services, middleware, and endpoints.</param>
    /// <returns>A configured <see cref="AppManager"/> ready to run.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modules"/> is <c>null</c>.</exception>
    public AppManager Build(AppModuleCollection modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var configure in _configureBuilder)
        {
            configure(_builder);
        }

        var app = ModulePipeline.Build(_builder, modules);

        return new AppManager(app);
    }
}
