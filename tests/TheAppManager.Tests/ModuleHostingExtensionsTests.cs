using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace TheAppManager.Tests;

public class ModuleHostingExtensionsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return builder;
    }

    [Fact]
    public async Task Phases_RunInOrderAcrossModules()
    {
        var log = new List<string>();
        await using var app = CreateBuilder()
            .AddModules(m => m.Add(new RecordingModule("A", log)).Add(new RecordingModule("B", log)))
            .Build();

        app.UseModules().MapModules();

        log.ShouldBe(["A.Services", "B.Services", "A.Middleware", "B.Middleware", "A.Endpoints", "B.Endpoints"]);
    }

    [Fact]
    public async Task MapModules_OnRouteGroup_MountsEndpointsUnderTheGroup()
    {
        await using var app = CreateBuilder().AddModules(m => m.Add<HelloModule>()).Build();
        app.UseModules();
        app.MapGroup("/api").MapModules();
        await app.StartAsync().WaitAsync(Timeout);

        using var client = app.GetTestClient();
        (await client.GetAsync("/api/hello").WaitAsync(Timeout)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/hello").WaitAsync(Timeout)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await app.StopAsync().WaitAsync(Timeout);
    }

    [Fact]
    public async Task NonWebModule_IsIgnoredByUseAndMapModules()
    {
        var log = new List<string>();
        await using var app = CreateBuilder().AddModules(m => m.Add(new ServicesOnlyModule(log))).Build();

        app.UseModules().MapModules();

        log.ShouldBe(["Services"]);
    }

    [Fact]
    public async Task UseAndMapModules_WithoutAddModules_Throw()
    {
        await using var app = CreateBuilder().Build();

        Should.Throw<InvalidOperationException>(() => app.UseModules());
        Should.Throw<InvalidOperationException>(() => app.MapModules());
    }

    [Fact]
    public async Task UseModules_Twice_Throws()
    {
        await using var app = CreateBuilder().AddModules(m => m.Add<HelloModule>()).Build();
        app.UseModules();

        Should.Throw<InvalidOperationException>(() => app.UseModules());
    }

    [Fact]
    public async Task MapModules_Twice_Throws_EvenOnADifferentTarget()
    {
        await using var app = CreateBuilder().AddModules(m => m.Add<HelloModule>()).Build();
        app.MapGroup("/api").MapModules();

        Should.Throw<InvalidOperationException>(() => app.MapModules());
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => ((WebApplication)null!).UseModules());
        Should.Throw<ArgumentNullException>(() => ((IEndpointRouteBuilder)null!).MapModules());
    }

    [AllowMultipleInstances]
    private sealed class RecordingModule(string name, List<string> log) : IWebModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => log.Add(name + ".Services");
        public void ConfigureMiddleware(WebApplication app) => log.Add(name + ".Middleware");
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => log.Add(name + ".Endpoints");
    }

    private sealed class ServicesOnlyModule(List<string> log) : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => log.Add("Services");
    }

    private sealed class HelloModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/hello", () => "hello");
    }
}
