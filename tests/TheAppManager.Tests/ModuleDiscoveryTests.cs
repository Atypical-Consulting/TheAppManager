using System.Reflection;
using TheAppManager.Modules;

namespace TheAppManager.Tests;

public class ModuleDiscoveryTests
{
    [Fact]
    public void DiscoverModules_WhenSomeTypesFailToLoad_ReturnsLoadableModules()
    {
        var modules = ModuleDiscovery.DiscoverModules(new PartiallyLoadableAssembly()).ToList();

        modules.Count.ShouldBe(1);
        modules[0].ShouldBeOfType<TestDiscoverableModule>();
    }

    private sealed class PartiallyLoadableAssembly : Assembly
    {
        public override Type[] GetTypes() =>
            throw new ReflectionTypeLoadException([typeof(TestDiscoverableModule), null], [null, new FileNotFoundException()]);
    }

    [Fact]
    public void DiscoverModules_FindsConcreteModules()
    {
        var modules = ModuleDiscovery.DiscoverModules(Assembly.GetExecutingAssembly()).ToList();

        modules.ShouldContain(m => m.GetType() == typeof(DiscoverableModule));
    }

    [Fact]
    public void DiscoverModules_ExcludesAbstractModules()
    {
        var modules = ModuleDiscovery.DiscoverModules(Assembly.GetExecutingAssembly()).ToList();

        modules.ShouldNotContain(m => m.GetType() == typeof(AbstractModule));
    }

    [Fact]
    public void DiscoverModules_ExcludesModulesWithoutParameterlessConstructor()
    {
        var modules = ModuleDiscovery.DiscoverModules(Assembly.GetExecutingAssembly()).ToList();

        modules.ShouldNotContain(m => m.GetType() == typeof(ModuleWithConstructorArgs));
    }

    [Fact]
    public void DiscoverModules_ThrowsOnNullAssembly()
    {
        Should.Throw<ArgumentNullException>(() => ModuleDiscovery.DiscoverModules(null!).ToList());
    }

    // Discoverable — concrete with parameterless constructor
    public class DiscoverableModule : IAppModule;

    // Should be excluded — abstract
    public abstract class AbstractModule : IAppModule;

    // Should be excluded — no parameterless constructor
    public class ModuleWithConstructorArgs(string name) : IAppModule
    {
        public string Name => name;
    }
}
