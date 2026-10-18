using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TheAppManager.Sample.Modules;

namespace TheAppManager.Sample.Tests;

/// <summary>The real app (Program.cs, /api group, auth) through WebApplicationFactory.</summary>
public sealed class WebAppTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebAppTests(WebApplicationFactory<Program> factory) => _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

    [Fact]
    public async Task Forecast_requires_a_token_then_succeeds_with_one()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/weatherforecast")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var login = await client.PostAsync("/api/login?user=alice", content: null);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/weatherforecast");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public async Task Production_skips_dev_only_modules()
    {
        await using var production = _factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

        (await production.CreateClient().PostAsync("/api/login?user=alice", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var plan = production.Services.GetRequiredService<ModulePlan>();
        plan.Single(e => e.ModuleType == typeof(DevLoginModule)).Applied.ShouldBeFalse();
        plan.Single(e => e.ModuleType == typeof(SwaggerModule)).Applied.ShouldBeFalse();
        plan.Single(e => e.ModuleType == typeof(WeatherModule)).Applied.ShouldBeTrue();
    }
}
