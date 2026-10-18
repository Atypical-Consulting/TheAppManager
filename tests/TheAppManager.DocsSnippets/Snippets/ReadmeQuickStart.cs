using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Compiled mirrors of the README quick start (checked by DocSnippetsTests).
namespace TheAppManager.DocsSnippets.Readme.QuickStart;

// begin-snippet: readme-modules
public sealed class ErrorHandlingModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddProblemDetails();

    public void ConfigureMiddleware(WebApplication app)
        => app.UseExceptionHandler();
}

public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddSingleton<WeatherService>();

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/weatherforecast", (WeatherService weather) => weather.Get());
}
// end-snippet

public static class Examples
{
    public static void Program(string[] args)
    {
        // begin-snippet: readme-program
        var builder = WebApplication.CreateBuilder(args);
        builder.AddModules(m => m
            .Add<ErrorHandlingModule>()
            .Add<WeatherModule>());

        var app = builder.Build();
        app.UseModules();
        app.MapModules();
        app.Run();
        // end-snippet
    }

    public static async Task Sugar(string[] args)
    {
        // begin-snippet: readme-runasync
        await AppManager.RunAsync(args, m => m
            .Add<ErrorHandlingModule>()
            .Add<WeatherModule>());
        // end-snippet
    }
}

public sealed class WeatherService
{
    public string[] Get() => ["sunny"];
}
