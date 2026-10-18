using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

// Compiled mirrors of the README composition and discovery sections (checked by DocSnippetsTests).
namespace TheAppManager.DocsSnippets.Readme.Composition;

// begin-snippet: readme-composition
public static class AppModules
{
    public static void Configure(AppModuleCollection modules) => modules
        .Add<ErrorHandlingModule>()
        .Add<AuthModule>()
        .AddIf<SwaggerModule>(b => b.Environment.IsDevelopment())
        .AddIf<MetricsModule>(b => b.Configuration.GetValue<bool>("Features:Metrics"))
        .Add<WeatherModule>();
}
// end-snippet

// begin-snippet: readme-multiple
[AllowMultipleInstances]
public sealed class HealthCheckModule(string name) : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddHealthChecks().AddCheck(name, () => HealthCheckResult.Healthy());
}
// end-snippet

public static class Examples
{
    public static void Replace(WebApplicationBuilder builder)
    {
        // begin-snippet: readme-replace
        var modules = new AppModuleCollection();
        AppModules.Configure(modules);
        modules.Replace<WeatherModule, CachedWeatherModule>();

        builder.AddModules(modules);
        // end-snippet
    }

    public static void Discovery(WebApplicationBuilder builder)
    {
        // begin-snippet: readme-discovery
        builder.AddModules(m => m.AddFromAssemblyOf<Program>());
        // end-snippet
    }
}

// Stubs the snippets reference.
public sealed class Program;
public sealed class ErrorHandlingModule : IWebModule;
public sealed class AuthModule : IWebModule;
public sealed class SwaggerModule : IWebModule;
public sealed class MetricsModule : IWebModule;
public sealed class WeatherModule : IWebModule;
public sealed class CachedWeatherModule : IWebModule;
