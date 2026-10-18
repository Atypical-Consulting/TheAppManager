using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TheAppManager.Modules;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

public class ReplaceModuleTests
{
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
        var modules = collection.GetModules();
        modules.Count.ShouldBe(3);
        modules[0].ShouldBeOfType<ModuleA>();
        modules[1].ShouldBeOfType<ReplacementModule>();
        modules[2].ShouldBeOfType<ModuleC>();
    }

    [Fact]
    public void Replace_ThrowsWhenModuleNotFound()
    {
        // Arrange
        var collection = new AppModuleCollection();
        collection.Add<ModuleA>();

        // Act & Assert
        Should.Throw<InvalidOperationException>(
            () => collection.Replace<ModuleB, ReplacementModule>());
    }

    [Fact]
    public void Replace_ReturnsSelfForChaining()
    {
        // Arrange
        var collection = new AppModuleCollection();
        collection.Add<ModuleA>();

        // Act
        var result = collection.Replace<ModuleA, ReplacementModule>();

        // Assert
        result.ShouldBeSameAs(collection);
    }

    [Fact]
    public void Replace_OnlyReplacesFirstOccurrence()
    {
        // Arrange
        var collection = new AppModuleCollection();
        collection.Add<ModuleA>().Add<ModuleA>().Add<ModuleC>();

        // Act
        collection.Replace<ModuleA, ReplacementModule>();

        // Assert
        var modules = collection.GetModules();
        modules.Count.ShouldBe(3);
        modules[0].ShouldBeOfType<ReplacementModule>();
        modules[1].ShouldBeOfType<ModuleA>();
        modules[2].ShouldBeOfType<ModuleC>();
    }

    private class RealService;
    private class FakeService;

    private class RealServiceModule : IAppModule
    {
        public void ConfigureServices(WebApplicationBuilder builder) => builder.Services.AddSingleton<RealService>();
    }

    private class FakeServiceModule : IAppModule
    {
        public void ConfigureServices(WebApplicationBuilder builder) => builder.Services.AddSingleton<FakeService>();
    }

    private class OrderedModule(string name, List<string> log) : IAppModule
    {
        public void ConfigureServices(WebApplicationBuilder builder) => log.Add(name);
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
    public void TestHost_Replace_ThrowsWhenModuleNotFound()
    {
        Should.Throw<InvalidOperationException>(
            () => new AppModuleTestHost().Replace<ModuleB, ReplacementModule>());
    }

    private sealed class RealProbeModule : IAppModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder app) => app.MapGet("/real", () => "real");
    }

    private sealed class FakeProbeModule : IAppModule
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
