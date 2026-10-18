using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TheAppManager.Testing;

// Compiled mirrors of the README testing section (checked by DocSnippetsTests). Not run: the modules are stubs.
namespace TheAppManager.DocsSnippets.Readme.Testing;

public static class Examples
{
    public static async Task Slice()
    {
        // begin-snippet: readme-testhost
        await using var host = await new AppModuleTestHost(m => m.Add<WeatherModule>())
            .ConfigureTestServices(s => s.AddSingleton<IWeatherSource, FakeWeatherSource>())
            .StartAsync();

        var response = await host.CreateClient().GetAsync("/weatherforecast");
        response.EnsureSuccessStatusCode();
        // end-snippet
    }

    public static async Task Production()
    {
        // begin-snippet: readme-testhost-production
        await using var host = await new AppModuleTestHost(AppModules.Configure).StartAsync();
        // end-snippet
    }

    public static async Task Factory()
    {
        // begin-snippet: readme-waf
        await using var factory = new WebApplicationFactory<Program>()
            .WithModules(m => m
                .Replace<WeatherModule, FakeWeatherModule>()
                .Without<SwaggerModule>());

        var response = await factory.CreateClient().GetAsync("/api/weatherforecast");
        response.EnsureSuccessStatusCode();
        // end-snippet
    }
}

// Stubs the snippets reference.
public sealed class Program;
public interface IWeatherSource;
public sealed class FakeWeatherSource : IWeatherSource;
public sealed class WeatherModule : IWebModule;
public sealed class FakeWeatherModule : IWebModule;
public sealed class SwaggerModule : IWebModule;

public static class AppModules
{
    public static void Configure(AppModuleCollection modules) => modules.Add<WeatherModule>();
}
