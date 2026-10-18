using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public void Resolve_ReturnsAppliedModulesInRegistrationOrder()
    {
        var a = new ModuleA();
        var c = new ModuleC();
        var collection = new AppModuleCollection()
            .Add(a)
            .AddIf<ModuleB>(_ => false)
            .Add(c);

        var modules = collection.Resolve(Builders.Empty());

        modules.ShouldBe([a, c]);
    }

    [Fact]
    public void Resolve_EvaluatesEachPredicateOnce()
    {
        var calls = 0;
        var collection = new AppModuleCollection().AddIf<ModuleA>(_ => { calls++; return true; });

        collection.Resolve(Builders.Empty());

        calls.ShouldBe(1);
    }

    [Fact]
    public void AddIf_Instance_AppliedWhenPredicateTrue()
    {
        var module = new ModuleA();

        var modules = new AppModuleCollection().AddIf(module, _ => true).Resolve(Builders.Empty());

        modules.ShouldHaveSingleItem().ShouldBeSameAs(module);
    }

    [Fact]
    public void AddIf_Instance_SkippedWhenPredicateFalse()
    {
        new AppModuleCollection().AddIf(new ModuleA(), _ => false).Resolve(Builders.Empty()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Development", 1)]
    [InlineData("Production", 0)]
    public void AddIf_Predicate_ReadsEnvironmentOnHostBuilder(string env, int expected)
    {
        var modules = new AppModuleCollection()
            .AddIf<ModuleA>(b => b.Environment.IsDevelopment())
            .Resolve(Builders.Empty(env));

        modules.Count.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_HostAgnosticModule_RunsAgainstNonWebBuilder()
    {
        var builder = Builders.Empty();

        foreach (var module in new AppModuleCollection().Add<ServiceModule>().Resolve(builder))
        {
            module.ConfigureServices(builder);
        }

        builder.Services.ShouldContain(d => d.ServiceType == typeof(ServiceModule));
    }

    public static TheoryData<string, Action<AppModuleCollection>> NullArguments => new()
    {
        { "Add", c => c.Add(null!) },
        { "AddIf<T>", c => c.AddIf<ModuleA>(null!) },
        { "AddIf(null, p)", c => c.AddIf(null!, _ => true) },
        { "AddIf(m, null)", c => c.AddIf(new ModuleA(), null!) },
        { "AddFromAssembly", c => c.AddFromAssembly(null!) },
        { "Resolve", c => c.Resolve(null!) },
    };

    [Theory]
    [MemberData(nameof(NullArguments))]
    public void NullArgument_Throws(string name, Action<AppModuleCollection> call)
    {
        name.ShouldNotBeNull();
        Should.Throw<ArgumentNullException>(() => call(new AppModuleCollection()));
    }

    [Fact]
    public void Resolve_DuplicateType_Throws()
    {
        var collection = new AppModuleCollection().Add<ModuleA>().Add(new ModuleA());

        var ex = Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()));

        ex.Message.ShouldContain(typeof(ModuleA).FullName!);
    }

    [Fact]
    public void Resolve_DuplicateType_ThrowsEvenWhenOneIsSkipped()
    {
        var collection = new AppModuleCollection().AddIf<ModuleA>(_ => true).AddIf<ModuleA>(_ => false);

        Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()))
            .Message.ShouldContain(typeof(ModuleA).FullName!);
    }

    [Fact]
    public void Resolve_AllowMultipleInstances_ReturnsBoth()
    {
        var first = new MultiModule();
        var second = new MultiModule();

        var modules = new AppModuleCollection().Add(first).Add(second).Resolve(Builders.Empty());

        modules.ShouldBe([first, second]);
    }

    [Fact]
    public void Resolve_AllowMultipleInstances_IsNotInherited()
    {
        var collection = new AppModuleCollection().Add(new DerivedMultiModule()).Add(new DerivedMultiModule());

        Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()));
    }

    [AllowMultipleInstances]
    private class MultiModule : IAppModule;

    private sealed class DerivedMultiModule : MultiModule;

    [Fact]
    public void FluentChaining_Works()
    {
        var collection = new AppModuleCollection()
            .Add<ModuleA>()
            .AddIf<ModuleB>(_ => true)
            .Add(new ModuleC());

        collection.Count().ShouldBe(3);
    }

    private sealed class ModuleA : IAppModule;
    private sealed class ModuleB : IAppModule;
    private sealed class ModuleC : IAppModule;

    private sealed class ServiceModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<ServiceModule>();
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
            new AppModuleCollection().AddIf<EmptyModule>((Func<IHostApplicationBuilder, bool>)null!));
    }

    [Fact]
    public void AddIf_Predicate_StillEnumerated()
    {
        new AppModuleCollection().AddIf<EmptyModule>(_ => false).Count().ShouldBe(1);
    }
}

/// <summary>Non-web host builders for exercising <see cref="AppModuleCollection.Resolve"/>.</summary>
internal static class Builders
{
    public static IHostApplicationBuilder Empty(string environment = "Production") =>
        Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
}
