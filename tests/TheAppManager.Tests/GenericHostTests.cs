using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TheAppManager.Tests;

public class GenericHostTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task AddModules_RegistersModuleServices_OnConsoleHost()
    {
        using var host = Host.CreateApplicationBuilder().AddModules(m => m.Add<ServiceModule>()).Build();

        host.Services.GetRequiredService<Marker>().ShouldNotBeNull();
        await host.StartAsync().WaitAsync(Timeout);
        await host.StopAsync().WaitAsync(Timeout);
    }

    [Fact]
    public async Task AddModules_WebModuleOnConsoleHost_GetsOnlyConfigureServices()
    {
        var log = new List<string>();
        using var host = Host.CreateApplicationBuilder().AddModules(m => m.Add(new WebRecordingModule(log))).Build();
        await host.StartAsync().WaitAsync(Timeout);
        await host.StopAsync().WaitAsync(Timeout);

        log.ShouldBe(["Services"]);
    }

    [Fact]
    public void ModulePlan_ListsEveryModuleInOrder_WithSkippedEntries()
    {
        using var host = Host.CreateApplicationBuilder()
            .AddModules(m => m.Add<ModuleA>().AddIf<SkippedModule>(_ => false).Add<ModuleB>())
            .Build();

        host.Services.GetRequiredService<ModulePlan>().ShouldBe(
        [
            new ModulePlanEntry(typeof(ModuleA), true),
            new ModulePlanEntry(typeof(SkippedModule), false),
            new ModulePlanEntry(typeof(ModuleB), true),
        ]);
    }

    [Fact]
    public async Task StartingTheHost_LogsThePlanOnceAtInformation()
    {
        var logs = new ListLoggerProvider();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddProvider(logs);
        builder.AddModules(m => m.Add<ModuleA>().AddIf<SkippedModule>(_ => false).Add<ModuleB>());
        using var host = builder.Build();

        await host.StartAsync().WaitAsync(Timeout);
        await host.StopAsync().WaitAsync(Timeout);

        var record = logs.Records.Where(r => r.Category == "TheAppManager" && r.Level == LogLevel.Information).ShouldHaveSingleItem();
        record.Message.ShouldBe(
            $"Module plan: 2 active [{typeof(ModuleA).FullName}, {typeof(ModuleB).FullName}]; " +
            $"1 skipped [{typeof(SkippedModule).FullName}]");
    }

    [Fact]
    public void AddModules_Twice_Throws()
    {
        var builder = Host.CreateApplicationBuilder().AddModules(m => m.Add<ModuleA>());

        Should.Throw<InvalidOperationException>(() => builder.AddModules(m => m.Add<ModuleB>()));
    }

    [Fact]
    public void AddModules_NullArguments_Throw()
    {
        var builder = Host.CreateApplicationBuilder();

        Should.Throw<ArgumentNullException>(() => ((HostApplicationBuilder)null!).AddModules(new AppModuleCollection()));
        Should.Throw<ArgumentNullException>(() => ((HostApplicationBuilder)null!).AddModules(_ => { }));
        Should.Throw<ArgumentNullException>(() => builder.AddModules((AppModuleCollection)null!));
        Should.Throw<ArgumentNullException>(() => builder.AddModules((Action<AppModuleCollection>)null!));
    }

    private sealed class Marker;

    private sealed class ServiceModule : IAppModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<Marker>();
    }

    private sealed class ModuleA : IAppModule;

    private sealed class ModuleB : IAppModule;

    private sealed class SkippedModule : IAppModule;

    private sealed class WebRecordingModule(List<string> log) : IWebModule
    {
        public void ConfigureServices(IHostApplicationBuilder builder) => log.Add("Services");
        public void ConfigureMiddleware(WebApplication app) => log.Add("Middleware");
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => log.Add("Endpoints");
    }
}
