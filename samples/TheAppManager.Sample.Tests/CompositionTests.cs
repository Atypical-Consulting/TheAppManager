using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using TheAppManager.Sample.Forecast;
using TheAppManager.Testing;

namespace TheAppManager.Sample.Tests;

/// <summary>Slice tests: the production composition on a TestServer (module endpoints at the root, no /api group).</summary>
public sealed class CompositionTests
{
    [Fact]
    public async Task Forecast_returns_the_configured_number_of_days()
    {
        await using var host = await new AppModuleTestHost(AppModules.Configure).StartAsync();

        var items = await host.CreateClient().GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");

        items!.Length.ShouldBe(5);
    }

    [Fact]
    public async Task Days_override_changes_the_item_count()
    {
        await using var host = await new AppModuleTestHost(AppModules.Configure)
            .ConfigureTestServices(s => s.Configure<WeatherOptions>(o => o.Days = 3))
            .StartAsync();

        var items = await host.CreateClient().GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");

        items!.Length.ShouldBe(3);
    }

    [Fact]
    public async Task Invalid_days_fail_the_host_start()
    {
        var host = new AppModuleTestHost(AppModules.Configure)
            .ConfigureTestServices(s => s.Configure<WeatherOptions>(o => o.Days = 0));

        var ex = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

        ex.Message.ShouldContain("Weather:Days");
    }

    [Fact]
    public async Task Unhandled_exception_in_production_is_a_problem_json_without_stack_frames()
    {
        await using var host = await new AppModuleTestHost(
            m =>
            {
                AppModules.Configure(m);
                m.Add<ThrowingModule>();
            },
            new WebApplicationOptions { EnvironmentName = "Production" }).StartAsync();

        var response = await host.CreateClient().GetAsync("/boom");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(" at ");
    }

    private sealed class ThrowingModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
            => endpoints.MapGet("/boom", () => { throw new InvalidOperationException("boom"); });
    }
}
