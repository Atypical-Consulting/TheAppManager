using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager.Tests;

public class AppModuleAmendmentsTests
{
    private static HostApplicationBuilder CreateBuilder(params string[] ids)
        => Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = ids.Select(id => $"--{AppModuleAmendments.ConfigurationSection}:{id}=true").ToArray(),
        });

    private static Type[] PlanTypes(HostApplicationBuilder builder)
        => builder.Build().Services.GetRequiredService<ModulePlan>().Select(e => e.ModuleType).ToArray();

    [Fact]
    public void RegisteredAmendment_IsAppliedBeforeResolve()
    {
        var id = AppModuleAmendments.Register(m => m.Replace<ModuleA, ModuleB>());
        var builder = CreateBuilder(id).AddModules(m => m.Add<ModuleA>());

        PlanTypes(builder).ShouldBe([typeof(ModuleB)]);
    }

    [Fact]
    public void TwoAmendments_ApplyInAscendingIdOrder()
    {
        var order = new List<string>();
        var first = AppModuleAmendments.Register(_ => order.Add("first"));
        var second = AppModuleAmendments.Register(_ => order.Add("second"));

        CreateBuilder(second, first).AddModules(m => m.Add<ModuleA>());

        order.ShouldBe(["first", "second"]);
    }

    [Fact]
    public void ReusedId_Throws()
    {
        var id = AppModuleAmendments.Register(_ => { });
        CreateBuilder(id).AddModules(m => m.Add<ModuleA>());

        Should.Throw<InvalidOperationException>(() => CreateBuilder(id).AddModules(m => m.Add<ModuleA>()))
            .Message.ShouldContain($"'{id}'");
    }

    [Fact]
    public void UnknownId_Throws()
    {
        Should.Throw<InvalidOperationException>(() => CreateBuilder("999999999").AddModules(m => m.Add<ModuleA>()))
            .Message.ShouldContain("'999999999'");
    }

    [Fact]
    public void Register_Null_Throws()
        => Should.Throw<ArgumentNullException>(() => AppModuleAmendments.Register(null!));

    private sealed class ModuleA : IAppModule;

    private sealed class ModuleB : IAppModule;
}
