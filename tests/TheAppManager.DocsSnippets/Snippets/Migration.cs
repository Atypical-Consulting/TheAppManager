using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using TheAppManager.Testing;

// Compiled mirrors of the C# blocks of docs/migration-v2-to-v3.md (checked by DocSnippetsTests).
namespace TheAppManager.DocsSnippets.Migration;

public static class Examples
{
    public static async Task ExplicitHosting(string[] args)
    {
        // begin-snippet: migration-program
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("custom-settings.json", optional: true); // was ConfigureBuilder
        builder.AddModules(m => m
            .Add<ErrorHandlingModule>()
            .Add<WeatherModule>());

        var app = builder.Build();
        app.UseModules();
        app.MapModules();
        await app.RunAsync();
        // end-snippet
    }

    public static async Task Sugar(string[] args)
    {
        // begin-snippet: migration-runasync
        await AppManager.RunAsync(args, m => m
            .Add<ErrorHandlingModule>()
            .Add<WeatherModule>());
        // end-snippet
    }

    public static async Task Discovery(string[] args)
    {
        // begin-snippet: migration-discovery
        await AppManager.RunAsync(args, m => m.AddFromAssemblyOf<Program>());
        // end-snippet
    }

    public static void Conditional(AppModuleCollection modules)
    {
        // begin-snippet: migration-addif
        modules.AddIf<SwaggerModule>(b => b.Environment.IsDevelopment());
        // end-snippet
    }

    public static void Duplicates(AppModuleCollection modules)
    {
        // begin-snippet: migration-duplicates-use
        modules
            .Add(new HealthCheckModule("db"))
            .Add(new HealthCheckModule("cache"));
        // end-snippet
    }

    public static async Task TestHost()
    {
        // begin-snippet: migration-testhost
        await using var host = await new AppModuleTestHost(m => m
                .Add<WeatherModule>()
                .Replace<WeatherModule, FakeWeatherModule>())
            .ConfigureTestServices(s => s.AddSingleton(TimeProvider.System))
            .StartAsync();

        var client = host.CreateClient();
        var time = host.Services.GetRequiredService<TimeProvider>();
        // end-snippet
        _ = (client, time);
    }

    public static async Task Factory()
    {
        // begin-snippet: migration-waf
        await using var factory = new WebApplicationFactory<Program>()
            .WithModules(m => m
                .Replace<WeatherModule, FakeWeatherModule>()
                .Without<SwaggerModule>());

        var client = factory.CreateClient();
        // end-snippet
        _ = client;
    }
}

// begin-snippet: migration-module
public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddSingleton<WeatherService>();

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/weatherforecast", (WeatherService weather) => weather.Get());
}
// end-snippet

// begin-snippet: migration-duplicates
[AllowMultipleInstances]
public sealed class HealthCheckModule(string name) : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddHealthChecks().AddCheck(name, () => HealthCheckResult.Healthy());
}
// end-snippet

// Stubs the snippets reference.
public sealed class Program;
public sealed class ErrorHandlingModule : IWebModule;
public sealed class SwaggerModule : IWebModule;
public sealed class FakeWeatherModule : IWebModule;

public sealed class WeatherService
{
    public string[] Get() => ["sunny"];
}
