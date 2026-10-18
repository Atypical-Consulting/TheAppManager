using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

public class AppModuleCollectionTests
{
    [Fact]
    public void Add_Generic_AddsModule()
    {
        var collection = new AppModuleCollection();

        collection.Add<EmptyModule>();

        collection.Count().ShouldBe(1);
    }

    [Fact]
    public void Add_Instance_AddsModule()
    {
        var collection = new AppModuleCollection();
        var module = new EmptyModule();

        collection.Add(module);

        collection.Count().ShouldBe(1);
        collection.First().ShouldBeSameAs(module);
    }

    [Fact]
    public void Add_Instance_ThrowsOnNull()
    {
        var collection = new AppModuleCollection();

        Should.Throw<ArgumentNullException>(() => collection.Add(null!));
    }

    [Fact]
    public void AddIf_True_AddsModule()
    {
        var collection = new AppModuleCollection();

        collection.AddIf<EmptyModule>(true);

        collection.Count().ShouldBe(1);
    }

    [Fact]
    public void AddIf_False_DoesNotAdd()
    {
        var collection = new AppModuleCollection();

        collection.AddIf<EmptyModule>(false);

        collection.ShouldBeEmpty();
    }

    [Fact]
    public void GetModules_PreservesRegistrationOrder()
    {
        var first = new EmptyModule();
        var second = new EmptyModule();
        var third = new EmptyModule();
        var collection = new AppModuleCollection();
        collection.Add(first);
        collection.Add(second);
        collection.Add(third);

        var modules = collection.GetModules();

        modules[0].ShouldBeSameAs(first);
        modules[1].ShouldBeSameAs(second);
        modules[2].ShouldBeSameAs(third);
    }

    [Fact]
    public void FluentChaining_Works()
    {
        var collection = new AppModuleCollection()
            .Add<EmptyModule>()
            .AddIf<EmptyModule>(true)
            .Add(new EmptyModule());

        collection.Count().ShouldBe(3);
    }

    private class EmptyModule : IAppModule;

    private sealed class ProbeModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder app) => app.MapGet("/probe", () => "ok");
    }

    private static async Task<System.Net.HttpStatusCode> ProbeAsync(AppModuleTestHost host)
    {
        await using var started = await host.StartAsync();
        return (await started.GetTestClient().GetAsync("/probe")).StatusCode;
    }

    [Theory]
    [InlineData("Development", System.Net.HttpStatusCode.OK)]
    [InlineData("Production", System.Net.HttpStatusCode.NotFound)]
    public async Task AddIf_Predicate_ReadsEnvironment(string env, System.Net.HttpStatusCode expected)
    {
        var host = new AppModuleTestHost()
            .ConfigureBuilder(b => b.Environment.EnvironmentName = env)
            .AddIf<ProbeModule>(b => b.Environment.IsDevelopment());

        (await ProbeAsync(host)).ShouldBe(expected);
    }

    [Fact]
    public async Task AddIf_Predicate_SeesConfigFromConfigureBuilder()
    {
        var host = new AppModuleTestHost()
            .ConfigureBuilder(b => b.Configuration["Probe:On"] = "true")
            .AddIf<ProbeModule>(b => b.Configuration.GetValue<bool>("Probe:On"));

        (await ProbeAsync(host)).ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddIf_Predicate_RunsOncePerBuild()
    {
        var calls = 0;
        var host = new AppModuleTestHost().AddIf<ProbeModule>(_ => { calls++; return true; });

        (await ProbeAsync(host)).ShouldBe(System.Net.HttpStatusCode.OK);
        calls.ShouldBe(1);
    }

    [Fact]
    public void AddIf_NullPredicate_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AppModuleCollection().AddIf<EmptyModule>((Func<WebApplicationBuilder, bool>)null!));
    }

    [Fact]
    public void AddIf_Predicate_StillEnumerated()
    {
        new AppModuleCollection().AddIf<EmptyModule>(_ => false).Count().ShouldBe(1);
    }
}
