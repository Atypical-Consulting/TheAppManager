using Microsoft.Extensions.Hosting;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

public class AppModuleTestHostTests
{
    [Fact]
    public async Task StartAsync_WithModule_EndpointResponds()
    {
        await using var host = new AppModuleTestHost();
        await host.Add<HelloModule>().StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/hello");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("Hello from test!");
    }

    [Fact]
    public async Task GetRequiredService_ReturnsRegisteredService()
    {
        await using var host = new AppModuleTestHost();
        await host.Add<ServiceModule>().StartAsync();

        var service = host.GetRequiredService<TestService>();

        service.ShouldNotBeNull();
        service.Name.ShouldBe("TestService");
    }

    [Fact]
    public void GetTestClient_BeforeStart_Throws()
    {
        var host = new AppModuleTestHost();

        Should.Throw<InvalidOperationException>(() => host.GetTestClient());
    }

    [Fact]
    public async Task ConfigureBuilder_IsApplied()
    {
        var configured = false;

        await using var host = new AppModuleTestHost();
        await host
            .ConfigureBuilder(_ => configured = true)
            .Add<HelloModule>()
            .StartAsync();

        configured.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_CleansUp()
    {
        var host = new AppModuleTestHost();
        await host.Add<HelloModule>().StartAsync();

        // Should not throw
        await host.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_RunsPhasesInOrderAcrossModules()
    {
        var log = new List<string>();
        await using var host = new AppModuleTestHost();
        await host.Add(new RecordingModule("A", log)).Add(new RecordingModule("B", log)).StartAsync();

        log.ShouldBe(ExpectedOrder);
    }

    [AllowMultipleInstances]
    private sealed class RecordingModule(string name, List<string> log) : IWebModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => log.Add(name + ".Services");
        public void ConfigureMiddleware(WebApplication app) => log.Add(name + ".Middleware");
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => log.Add(name + ".Endpoints");
    }

    private static readonly string[] ExpectedOrder =
        ["A.Services", "B.Services", "A.Middleware", "B.Middleware", "A.Endpoints", "B.Endpoints"];

    private class HelloModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/hello", () => "Hello from test!");
        }
    }

    private class ServiceModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton(new TestService { Name = "TestService" });
        }
    }

    private class TestService
    {
        public string Name { get; init; } = string.Empty;
    }
}
