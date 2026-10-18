using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TheAppManager.Testing;
using TheAppManager.Testing.WebApp;

namespace TheAppManager.Tests;

public class WebApplicationFactoryModuleTests
{
    private static async Task<string> GetPingAsync(WebApplicationFactory<Program> factory)
        => await factory.CreateClient().GetStringAsync("/ping");

    private static Type[] PlanTypes(WebApplicationFactory<Program> factory)
        => factory.Services.GetRequiredService<ModulePlan>().Select(e => e.ModuleType).ToArray();

    [Fact]
    public async Task Replace_SwapsTheRealModule()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithModules(m => m.Replace<PingModule, FakePingModule>());

        (await GetPingAsync(factory)).ShouldBe("fake");
        PlanTypes(factory).ShouldContain(typeof(FakePingModule));
        PlanTypes(factory).ShouldNotContain(typeof(PingModule));
    }

    [Fact]
    public async Task Without_RemovesTheModule_AndLeavesPlainFactoriesUntouched()
    {
        await using var amended = new WebApplicationFactory<Program>().WithModules(m => m.Without<ExtraModule>());
        (await amended.CreateClient().GetAsync("/extra")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using var plain = new WebApplicationFactory<Program>();
        (await plain.CreateClient().GetAsync("/extra")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetPingAsync(plain)).ShouldBe("real");
    }

    [Fact]
    public async Task ChainedAmendments_ApplyInCallOrder_AndSurviveDerivedFactories()
    {
        // Replace overwrites an earlier substitution of the same type, so the last applied wins.
        await using var factory = new WebApplicationFactory<Program>()
            .WithModules(m => m.Replace<PingModule>(new ValuePingModule("first")))
            .WithModules(m => m.Replace<PingModule>(new ValuePingModule("second")));
        (await GetPingAsync(factory)).ShouldBe("second");

        await using var derived = factory.WithWebHostBuilder(_ => { });
        (await GetPingAsync(derived)).ShouldBe("second");
    }

    [Fact]
    public async Task ConcurrentFactories_EachSeeOnlyTheirOwnAmendment()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithModules(m => m.Replace<PingModule>(new ValuePingModule($"v{i}")));
            return await GetPingAsync(factory);
        })));

        results.ShouldBe(Enumerable.Range(0, 16).Select(i => $"v{i}").ToArray());
    }

    private sealed class FakePingModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/ping", () => "fake");
    }

    private sealed class ValuePingModule(string value) : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/ping", () => value);
    }
}

[CollectionDefinition(nameof(ProcessEnvironmentCollection), DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection;

// Mutates a process-wide environment variable, so it runs alone.
[Collection(nameof(ProcessEnvironmentCollection))]
public class WebApplicationFactoryArgsTests
{
    [Fact]
    public void ProgramIgnoringArgs_FailsLoudly()
    {
        Environment.SetEnvironmentVariable("NO_ARGS", "1");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithModules(m => m.Without<ExtraModule>());
            Should.Throw<InvalidOperationException>(() => factory.CreateClient()).Message.ShouldContain("args");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_ARGS", null);
        }
    }
}
