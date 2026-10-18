using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace TheAppManager.Tests;

public class AppManagerTests
{
    [Fact]
    public void Start_ThrowsOnNullArgs()
    {
        Should.Throw<ArgumentNullException>(() => AppManager.Start(null!, _ => { }));
    }

    [Fact]
    public async Task StartAsync_ThrowsOnNullConfigure()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => AppManager.StartAsync([], null!));
    }

    [Fact]
    public async Task StartAsync_ThrowsOnNullArgs()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => AppManager.StartAsync(null!, _ => { }));
    }

    [Fact]
    public async Task RunAsync_WithToken_CompletesWhenCancelled()
    {
        var app = new AppManagerBuilder(["--urls", "http://127.0.0.1:0"]).Build(new AppModuleCollection());
        using var cts = new CancellationTokenSource();

        var run = app.RunAsync(cts.Token);
        await cts.CancelAsync();

        await run.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task StartAsync_WithModules_RunsAndCompletes()
    {
        await AppManager.StartAsync([], modules =>
        {
            modules.Add(new ShutdownModule());
        });
    }

    [Fact]
    public async Task StartAsync_WithConfigureBuilder_AppliesConfiguration()
    {
        var builderConfigured = false;

        await AppManager.StartAsync([],
            modules => modules.Add(new ShutdownModule()),
            _ => builderConfigured = true);

        builderConfigured.ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_WithEndpointModule_RegistersEndpoints()
    {
        var endpointConfigured = false;

        await AppManager.StartAsync([], modules =>
        {
            modules.Add(new EndpointTrackingModule(() => endpointConfigured = true));
            modules.Add(new ShutdownModule());
        });

        endpointConfigured.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_CompletesWhenApplicationStops()
    {
        var modules = new AppModuleCollection();
        modules.Add(new ShutdownModule());

        var appManager = new AppManagerBuilder([]).Build(modules);

        await appManager.RunAsync();
    }

    private class ShutdownModule : IWebModule
    {
        public void ConfigureMiddleware(WebApplication app)
        {
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                app.Lifetime.StopApplication();
            });
        }
    }

    private class EndpointTrackingModule(Action onConfigure) : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            onConfigure();
            endpoints.MapGet("/track", () => "tracked");
        }
    }
}
