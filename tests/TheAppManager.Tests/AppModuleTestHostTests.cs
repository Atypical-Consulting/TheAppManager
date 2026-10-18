using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

public class AppModuleTestHostTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task StartAsync_WithModule_EndpointResponds()
    {
        await using var host = new AppModuleTestHost();
        await host.Add<HelloModule>().StartAsync().WaitAsync(Timeout);

        var client = host.GetTestClient();
        var response = await client.GetAsync("/hello").WaitAsync(Timeout);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("Hello from test!");
    }

    [Fact]
    public async Task GetRequiredService_ReturnsRegisteredService()
    {
        await using var host = new AppModuleTestHost();
        await host.Add<ServiceModule>().StartAsync().WaitAsync(Timeout);

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
            .StartAsync().WaitAsync(Timeout);

        configured.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheServiceProvider()
    {
        var host = new AppModuleTestHost();
        await host.Add<ServiceModule>().StartAsync().WaitAsync(Timeout);
        host.GetRequiredService<TestService>().ShouldNotBeNull();

        await host.DisposeAsync();

        Should.Throw<ObjectDisposedException>(() => host.GetRequiredService<TestService>());
    }

    [Fact]
    public async Task StartAsync_MiddlewareFailure_DisposesTheAppAndRethrows()
    {
        var module = new FailingMiddlewareModule();
        await using var host = new AppModuleTestHost().Add(module);

        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync().WaitAsync(Timeout));

        module.Probe.ShouldNotBeNull().Disposed.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => host.GetTestClient());
    }

    private sealed class FailingMiddlewareModule : IWebModule
    {
        public DisposableProbe? Probe { get; private set; }

        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<DisposableProbe>();

        public void ConfigureMiddleware(WebApplication app)
        {
            Probe = app.Services.GetRequiredService<DisposableProbe>();
            throw new InvalidOperationException("middleware failed");
        }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task StartAsync_RunsPhasesInOrderAcrossModules()
    {
        var log = new List<string>();
        await using var host = new AppModuleTestHost();
        await host.Add(new RecordingModule("A", log)).Add(new RecordingModule("B", log)).StartAsync().WaitAsync(Timeout);

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
