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
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());
        await host.StartAsync().WaitAsync(Timeout);

        using var client = host.CreateClient();
        var response = await client.GetAsync("/hello").WaitAsync(Timeout);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("Hello from test!");
    }

    [Fact]
    public async Task Services_ResolvesRegisteredService()
    {
        await using var host = new AppModuleTestHost(m => m.Add<ServiceModule>());
        await host.StartAsync().WaitAsync(Timeout);

        var service = host.Services.GetRequiredService<TestService>();

        service.Name.ShouldBe("TestService");
    }

    [Fact]
    public async Task CreateClient_BeforeStart_Throws()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());

        Should.Throw<InvalidOperationException>(() => host.CreateClient());
    }

    [Fact]
    public void Constructors_NullArgument_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new AppModuleTestHost((AppModuleCollection)null!));
        Should.Throw<ArgumentNullException>(() => new AppModuleTestHost((Action<AppModuleCollection>)null!));
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheServiceProvider_AndIsIdempotent()
    {
        var host = new AppModuleTestHost(m => m.Add<ServiceModule>());
        await host.StartAsync().WaitAsync(Timeout);
        var services = host.Services;

        await host.DisposeAsync();
        await host.DisposeAsync();

        Should.Throw<ObjectDisposedException>(() => services.GetRequiredService<TestService>());
        Should.Throw<InvalidOperationException>(() => host.Services);
    }

    [Fact]
    public async Task StartAsync_Twice_Throws()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());
        await host.StartAsync().WaitAsync(Timeout);

        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync().WaitAsync(Timeout));
    }

    [Fact]
    public async Task StartAsync_AfterAFailedStart_Throws()
    {
        await using var host = new AppModuleTestHost(m => m.Add(new FailingMiddlewareModule()));
        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync().WaitAsync(Timeout));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync().WaitAsync(Timeout));
        ex.Message.ShouldContain("single-use");
    }

    [Fact]
    public async Task ConfigureTestServices_AfterStart_Throws()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());
        await host.StartAsync().WaitAsync(Timeout);

        Should.Throw<InvalidOperationException>(() => host.ConfigureTestServices(_ => { }));
    }

    [Fact]
    public async Task Services_BeforeStart_Throws()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());

        Should.Throw<InvalidOperationException>(() => host.Services);
    }

    [Fact]
    public async Task StartAsync_MiddlewareFailure_DisposesTheAppAndRethrows()
    {
        var module = new FailingMiddlewareModule();
        await using var host = new AppModuleTestHost(m => m.Add(module));

        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync().WaitAsync(Timeout));

        module.Probe.ShouldNotBeNull().Disposed.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => host.CreateClient());
    }

    [Fact]
    public async Task Environment_DefaultsToDevelopment()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());
        await host.StartAsync().WaitAsync(Timeout);

        host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName.ShouldBe(Environments.Development);
    }

    [Fact]
    public async Task Options_EnvironmentAndApplicationName_AreHonoured()
    {
        await using var host = new AppModuleTestHost(
            m => m.Add<HelloModule>(),
            new WebApplicationOptions { EnvironmentName = "Staging", ApplicationName = "X" });
        await host.StartAsync().WaitAsync(Timeout);

        var env = host.Services.GetRequiredService<IHostEnvironment>();
        env.EnvironmentName.ShouldBe("Staging");
        env.ApplicationName.ShouldBe("X");
    }

    [Fact]
    public async Task ScopedService_FromTheRoot_ThrowsEvenInProduction()
    {
        await using var host = new AppModuleTestHost(
            m => m.Add<ScopedModule>(),
            new WebApplicationOptions { EnvironmentName = Environments.Production });
        await host.StartAsync().WaitAsync(Timeout);

        Should.Throw<InvalidOperationException>(() => host.Services.GetRequiredService<ScopedThing>());
    }

    [Fact]
    public async Task CaptiveDependency_FailsStart()
    {
        await using var host = new AppModuleTestHost(m => m.Add<CaptiveModule>());

        await Should.ThrowAsync<AggregateException>(() => host.StartAsync().WaitAsync(Timeout));
    }

    [Fact]
    public async Task ConfigureTestServices_RunsAfterTheModules()
    {
        await using var host = new AppModuleTestHost(m => m.Add<ClockModule>())
            .ConfigureTestServices(s => s.AddSingleton<IClock, FakeClock>());
        await host.StartAsync().WaitAsync(Timeout);

        host.Services.GetRequiredService<IClock>().ShouldBeOfType<FakeClock>();
    }

    [Fact]
    public async Task ConfigureTestServices_Null_Throws()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());

        Should.Throw<ArgumentNullException>(() => host.ConfigureTestServices(null!));
    }

    private sealed class ScopedThing;

    private sealed class ScopedModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<ScopedThing>();
    }

    private sealed class Captive(ScopedThing thing)
    {
        public ScopedThing Thing { get; } = thing;
    }

    private sealed class CaptiveModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) =>
            builder.Services.AddScoped<ScopedThing>().AddSingleton<Captive>();
    }

    private interface IClock;

    private sealed class RealClock : IClock;

    private sealed class FakeClock : IClock;

    private sealed class ClockModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<IClock, RealClock>();
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
        await using var host = new AppModuleTestHost(m => m.Add(new RecordingModule("A", log)).Add(new RecordingModule("B", log)));
        await host.StartAsync().WaitAsync(Timeout);

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
