using Microsoft.AspNetCore.Builder;

namespace TheAppManager.Modules;

/// <summary>
/// The single copy of the module pipeline, shared by production startup and the test host.
/// </summary>
internal static class ModulePipeline
{
    /// <summary>
    /// Runs every ConfigureServices, builds the app, then every ConfigureMiddleware and ConfigureEndpoints.
    /// </summary>
    internal static WebApplication Build(WebApplicationBuilder builder, AppModuleCollection modules)
    {
        var registered = modules.GetModules();

        foreach (var module in registered)
        {
            module.ConfigureServices(builder);
        }

        var app = builder.Build();

        foreach (var module in registered)
        {
            module.ConfigureMiddleware(app);
        }

        foreach (var module in registered)
        {
            module.ConfigureEndpoints(app);
        }

        return app;
    }
}
