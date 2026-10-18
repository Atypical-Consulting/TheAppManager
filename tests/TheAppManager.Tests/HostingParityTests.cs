using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

/// <summary>The same composition hosted the production way and through the test host yields the same app.</summary>
public class HostingParityTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static void Configure(AppModuleCollection m) => m
        .Add<AModule>()
        .Add<BModule>()
        .AddIf<Skipped>(_ => false);

    [Fact]
    public async Task ProductionHosting_AndTestHost_YieldTheSameEndpointsPlanAndServices()
    {
        await using var prod = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" })
            .AddModules(Configure)
            .Build();
        prod.UseModules();
        prod.MapModules();
        // The global EndpointDataSource is only populated once the pipeline is built, i.e. at start.
        prod.Urls.Add("http://127.0.0.1:0");
        await prod.StartAsync().WaitAsync(Timeout);

        await using var test = new AppModuleTestHost(Configure);
        await test.StartAsync().WaitAsync(Timeout);

        Routes(test.Services).ShouldBe(Routes(prod.Services));
        Routes(prod.Services).ShouldBe(["/a", "/b/{id}"]);
        Plan(test.Services).ShouldBe(Plan(prod.Services));
        test.Services.GetRequiredService<IGreeter>().GetType()
            .ShouldBe(prod.Services.GetRequiredService<IGreeter>().GetType());

        await prod.StopAsync().WaitAsync(Timeout);
    }

    private static string?[] Routes(IServiceProvider services) =>
        services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static (Type, bool)[] Plan(IServiceProvider services) =>
        services.GetRequiredService<ModulePlan>().Select(e => (e.ModuleType, e.Applied)).ToArray();

    private interface IGreeter;

    private sealed class Greeter : IGreeter;

    private sealed class AModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/a", () => "a");
    }

    private sealed class BModule : IWebModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<IGreeter, Greeter>();

        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/b/{id}", (int id) => Results.Ok(id));
    }

    private sealed class Skipped : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/skipped", () => "x");
    }
}
