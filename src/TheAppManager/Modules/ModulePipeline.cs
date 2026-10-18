using Microsoft.AspNetCore.Builder;

namespace TheAppManager;

/// <summary>
/// The single copy of the module pipeline, shared by production startup and the test host.
/// </summary>
internal static class ModulePipeline
{
    /// <summary>
    /// Runs every ConfigureServices, builds the app, then ConfigureMiddleware and ConfigureEndpoints on every <see cref="IWebModule"/>.
    /// </summary>
    internal static WebApplication Build(WebApplicationBuilder builder, AppModuleCollection modules)
    {
        var registered = modules.GetModules(builder);

        foreach (var module in registered)
        {
            module.ConfigureServices(builder);
        }

        var app = builder.Build();

        var web = registered.OfType<IWebModule>().ToList();

        foreach (var module in web)
        {
            module.ConfigureMiddleware(app);
        }

        foreach (var module in web)
        {
            module.ConfigureEndpoints(app);
        }

        return app;
    }
}
