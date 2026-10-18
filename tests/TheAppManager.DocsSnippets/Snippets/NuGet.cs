using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheAppManager.Testing;

// Compiled mirrors of the packages' NUGET.md files (checked by DocSnippetsTests).
namespace TheAppManager.DocsSnippets.NuGet;

// begin-snippet: nuget-core-module
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
        // begin-snippet: nuget-core-program
        var builder = WebApplication.CreateBuilder(args);
        builder.AddModules(m => m.Add<WeatherModule>());

        var app = builder.Build();
        app.UseModules();
        app.MapModules();
        app.Run();
        // end-snippet
    }

    public static async Task Testing()
    {
        // begin-snippet: nuget-testing
        await using var host = await new AppModuleTestHost(m => m.Add<WeatherModule>())
            .ConfigureTestServices(s => s.AddSingleton<IWeatherSource, FakeWeatherSource>())
            .StartAsync();

        var response = await host.CreateClient().GetAsync("/weatherforecast");

        await using var factory = new WebApplicationFactory<Program>()
            .WithModules(m => m.Replace<WeatherModule, FakeWeatherModule>());
        // end-snippet
        _ = response;
    }
}

// Stubs the snippets reference.
public sealed class Program;
public interface IWeatherSource;
public sealed class FakeWeatherSource : IWeatherSource;
public sealed class FakeWeatherModule : IWebModule;

public sealed class WeatherService
{
    public string[] Get() => ["sunny"];
}
