using Microsoft.Extensions.Hosting;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace TheAppManager;

/// <summary>
/// Manages the lifecycle of an ASP.NET Core web application.
/// Use <see cref="Start"/> or <see cref="StartAsync"/> for a simplified startup experience,
/// or use <see cref="AppManagerBuilder"/> for more control.
/// </summary>
public sealed class AppManager
{
    private readonly WebApplication _app;

    internal AppManager(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _app = app;
    }

    /// <summary>
    /// Runs the web application synchronously.
    /// </summary>
    public void Run()
    {
        _app.Run();
    }

    /// <summary>
    /// Runs the web application asynchronously.
    /// </summary>
    /// <returns>A task that represents the lifetime of the application.</returns>
    public Task RunAsync()
    {
        return _app.RunAsync();
    }

    /// <summary>
    /// Runs the web application asynchronously until it shuts down or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Triggers the application to stop when cancelled.</param>
    /// <returns>A task that represents the lifetime of the application.</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        return ((IHost)_app).RunAsync(cancellationToken);
    }

    /// <summary>
    /// Creates, configures, and runs a web application using the provided modules.
    /// When no modules are configured, modules are auto-discovered from the entry assembly.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="configure">
    /// An optional callback to register modules. If <c>null</c>, modules are discovered automatically
    /// from the entry assembly.
    /// </param>
    /// <param name="configureBuilder">
    /// An optional callback to configure the <see cref="WebApplicationBuilder"/> before building.
    /// </param>
    public static void Start(
        string[] args,
        Action<AppModuleCollection>? configure = null,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        var modules = ResolveModules(configure);
        var appManager = new AppManagerBuilder(args)
            .ConfigureBuilder(configureBuilder)
            .Build(modules);
        appManager.Run();
    }

    /// <summary>
    /// Creates, configures, and runs a web application asynchronously using the provided modules.
    /// When no modules are configured, modules are auto-discovered from the entry assembly.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="configure">
    /// An optional callback to register modules. If <c>null</c>, modules are discovered automatically
    /// from the entry assembly.
    /// </param>
    /// <param name="configureBuilder">
    /// An optional callback to configure the <see cref="WebApplicationBuilder"/> before building.
    /// </param>
    /// <returns>A task that represents the lifetime of the application.</returns>
    public static Task StartAsync(
        string[] args,
        Action<AppModuleCollection>? configure = null,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        var modules = ResolveModules(configure);
        var appManager = new AppManagerBuilder(args)
            .ConfigureBuilder(configureBuilder)
            .Build(modules);
        return appManager.RunAsync();
    }

    private static AppModuleCollection ResolveModules(Action<AppModuleCollection>? configure)
    {
        var modules = new AppModuleCollection();

        if (configure is not null)
        {
            configure(modules);
        }
        else
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly is not null)
            {
                foreach (var module in ModuleDiscovery.DiscoverModules(entryAssembly))
                {
                    modules.Add(module);
                }
            }
        }

        return modules;
    }
}
