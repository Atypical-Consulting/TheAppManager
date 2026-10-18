using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

public class ReplaceModuleTests
{
    [AllowMultipleInstances]
    private class ModuleA : IAppModule;
    private class ModuleB : IAppModule;
    private class ModuleC : IAppModule;
    private class ReplacementModule : IAppModule;

    [Fact]
    public void Replace_SwapsModuleAtSamePosition()
    {
        // Arrange
        var collection = new AppModuleCollection();
        collection.Add<ModuleA>().Add<ModuleB>().Add<ModuleC>();

        // Act
        collection.Replace<ModuleB, ReplacementModule>();

        // Assert
        var modules = collection.Resolve(Builders.Empty());
        modules.Count.ShouldBe(3);
        modules[0].ShouldBeOfType<ModuleA>();
        modules[1].ShouldBeOfType<ReplacementModule>();
        modules[2].ShouldBeOfType<ModuleC>();
    }

    [Fact]
    public void Replace_UnregisteredType_ThrowsAtResolveNotAtCall()
    {
        var collection = new AppModuleCollection().Add<ModuleA>();

        collection.Replace<ModuleB, ReplacementModule>();

        Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()))
            .Message.ShouldContain(typeof(ModuleB).FullName!);
    }

    [Fact]
    public void Replace_ReturnsSelfForChaining()
    {
        var collection = new AppModuleCollection().Add<ModuleA>();

        collection.Replace<ModuleA, ReplacementModule>().ShouldBeSameAs(collection);
        collection.Replace<ModuleA>(new ReplacementModule()).ShouldBeSameAs(collection);
    }

    [Fact]
    public void Replace_AllowMultiple_ReplacesEveryOccurrence()
    {
        var replacement = new MultiReplacement();
        var collection = new AppModuleCollection().Add<ModuleA>().Add<ModuleA>().Add<ModuleC>();

        collection.Replace<ModuleA>(replacement);

        var modules = collection.Resolve(Builders.Empty());
        modules.Count.ShouldBe(3);
        modules[0].ShouldBeSameAs(replacement);
        modules[1].ShouldBeSameAs(replacement);
        modules[2].ShouldBeOfType<ModuleC>();
    }

    [Fact]
    public void Replace_BeforeAdd_IsApplied()
    {
        var modules = new AppModuleCollection()
            .Replace<ModuleA, ReplacementModule>()
            .Add<ModuleA>()
            .Resolve(Builders.Empty());

        modules.ShouldHaveSingleItem().ShouldBeOfType<ReplacementModule>();
    }

    [Fact]
    public void Replace_SurvivesLaterScan()
    {
        var modules = new AppModuleCollection()
            .Add<TestDiscoverableModule>()
            .Replace<TestDiscoverableModule, ReplacementModule>()
            .AddFromAssemblyOf<TestDiscoverableModule>()
            .Resolve(Builders.Empty());

        modules.ShouldNotContain(m => m is TestDiscoverableModule);
        modules.Count(m => m is ReplacementModule).ShouldBe(1);
    }

    [Fact]
    public void Replace_Instance_KeepsPositionAndPredicate()
    {
        var replacement = new ReplacementModule();
        var collection = new AppModuleCollection()
            .Add<ModuleB>()
            .AddIf<ModuleA>(_ => true)
            .AddIf<ModuleC>(_ => false)
            .Replace<ModuleA>(replacement)
            .Replace<ModuleC, OtherReplacement>();

        var modules = collection.Resolve(Builders.Empty());

        modules.Count.ShouldBe(2);
        modules[0].ShouldBeOfType<ModuleB>();
        modules[1].ShouldBeSameAs(replacement);
    }

    [Fact]
    public void Replace_LastWins()
    {
        var modules = new AppModuleCollection()
            .Add<ModuleA>()
            .Replace<ModuleA, ReplacementModule>()
            .Replace<ModuleA, OtherReplacement>()
            .Resolve(Builders.Empty());

        modules.ShouldHaveSingleItem().ShouldBeOfType<OtherReplacement>();
    }

    [Fact]
    public void Replace_ExactTypeOnly()
    {
        var collection = new AppModuleCollection().Add<DerivedModuleB>().Replace<ModuleB, ReplacementModule>();

        Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()));
    }

    [Fact]
    public void Replace_WithAlreadyRegisteredType_ThrowsDuplicate()
    {
        var collection = new AppModuleCollection().Add<ModuleB>().Add<ReplacementModule>().Replace<ModuleB, ReplacementModule>();

        Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()))
            .Message.ShouldContain(typeof(ReplacementModule).FullName!);
    }

    [Fact]
    public void Replace_NullInstance_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new AppModuleCollection().Replace<ModuleA>(null!));
    }

    [Fact]
    public void Enumeration_YieldsRegistrationsAsDeclared()
    {
        var collection = new AppModuleCollection().Add<ModuleB>().Replace<ModuleB, ReplacementModule>();

        collection.ShouldHaveSingleItem().ShouldBeOfType<ModuleB>();
    }

    private class DerivedModuleB : ModuleB;
    private sealed class OtherReplacement : IAppModule;

    [AllowMultipleInstances]
    private sealed class MultiReplacement : IAppModule;

    private class RealService;
    private class FakeService;

    private class RealServiceModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<RealService>();
    }

    private class FakeServiceModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<FakeService>();
    }

    [AllowMultipleInstances]
    private class OrderedModule(string name, List<string> log) : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => log.Add(name);
    }

    [Fact]
    public async Task TestHost_Replace_RegistersFakeNotReal()
    {
        await using var host = await new AppModuleTestHost()
            .Add<RealServiceModule>()
            .Replace<RealServiceModule, FakeServiceModule>()
            .StartAsync();

        host.GetRequiredService<FakeService>().ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => host.GetRequiredService<RealService>());
    }

    [Fact]
    public async Task TestHost_Replace_KeepsPosition()
    {
        var log = new List<string>();
        await using var host = await new AppModuleTestHost()
            .Add(new OrderedModule("first", log))
            .Add<RealServiceModule>()
            .Add(new OrderedModule("last", log))
            .Replace<RealServiceModule, FakeServiceModule>()
            .StartAsync();

        // Fake sits between first and last: its service is registered and order is preserved.
        log.ShouldBe(["first", "last"]);
        host.GetRequiredService<FakeService>().ShouldNotBeNull();
    }

    [Fact]
    public async Task TestHost_Replace_ThrowsWhenModuleNotFound()
    {
        await using var host = new AppModuleTestHost().Replace<ModuleB, ReplacementModule>();

        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
    }

    private sealed class RealProbeModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder app) => app.MapGet("/real", () => "real");
    }

    private sealed class FakeProbeModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder app) => app.MapGet("/fake", () => "fake");
    }

    [Theory]
    [InlineData(false, System.Net.HttpStatusCode.NotFound)]
    [InlineData(true, System.Net.HttpStatusCode.OK)]
    public async Task Replace_KeepsTheCondition(bool on, System.Net.HttpStatusCode expected)
    {
        await using var host = await new AppModuleTestHost()
            .AddIf<RealProbeModule>(_ => on)
            .Replace<RealProbeModule, FakeProbeModule>()
            .StartAsync();

        (await host.GetTestClient().GetAsync("/fake")).StatusCode.ShouldBe(expected);
    }
}
