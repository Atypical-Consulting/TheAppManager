using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TheAppManager.Tests;

/// <summary>
/// A simple discoverable module used by multi-assembly scanning tests.
/// </summary>
public class TestDiscoverableModule : IAppModule;

[AllowMultipleInstances]
public class RepeatableDiscoverableModule : IAppModule;

public class ScanReplacementModule : IAppModule;

public class MultiAssemblyScanningTests
{
    [Fact]
    public void AddFromAssemblyOf_AcceptsAnyAnchorType_AndScansItsAssembly()
    {
        var collection = new AppModuleCollection();

        collection.AddFromAssemblyOf<MultiAssemblyScanningTests>();

        collection.ShouldContain(m => m.GetType() == typeof(TestDiscoverableModule));
    }

    [Fact]
    public void AddFromAssembly_DiscoversModules()
    {
        var collection = new AppModuleCollection();

        collection.AddFromAssembly(Assembly.GetExecutingAssembly());

        collection.ShouldContain(m => m.GetType() == typeof(TestDiscoverableModule));
    }

    [Fact]
    public void AddFromAssembly_ThrowsOnNull()
    {
        Should.Throw<ArgumentNullException>(() => new AppModuleCollection().AddFromAssembly(null!));
    }

    [Fact]
    public void AddFromAssemblyOf_ReturnsSelfForChaining()
    {
        var collection = new AppModuleCollection();

        collection.AddFromAssemblyOf<TestDiscoverableModule>().ShouldBeSameAs(collection);
    }

    [Theory]
    [InlineData(nameof(AppModuleCollection.AddFromAssembly))]
    [InlineData(nameof(AppModuleCollection.AddFromAssemblyOf))]
    public void ScanningMethods_RequireUnreferencedCode(string name)
    {
        typeof(AppModuleCollection).GetMethod(name)!
            .GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldNotBeNull();
    }

    [Fact]
    public void AddFromAssembly_EnumeratingTwice_YieldsSameInstances()
    {
        var collection = new AppModuleCollection().AddFromAssembly(new StubAssembly(typeof(TestDiscoverableModule)));

        collection.ToList().ShouldBe(collection.ToList());
        collection.Single().ShouldBeSameAs(collection.Single());
    }

    [Fact]
    public void AddFromAssembly_WithNoDiscoverableModule_Throws()
    {
        var ex = Should.Throw<InvalidOperationException>(() => new AppModuleCollection().AddFromAssembly(new StubAssembly()));

        ex.Message.ShouldContain("Stub.Assembly");
    }

    [Fact]
    public void AddFromAssembly_DuplicateOfExplicitAdd_ThrowsAtResolve()
    {
        var collection = new AppModuleCollection()
            .Add<TestDiscoverableModule>()
            .AddFromAssembly(new StubAssembly(typeof(TestDiscoverableModule)));

        var ex = Should.Throw<InvalidOperationException>(() => collection.Resolve(Builders.Empty()));
        ex.Message.ShouldContain(nameof(TestDiscoverableModule));
    }

    [Fact]
    public void AddFromAssembly_DuplicateWithAllowMultipleInstances_ResolvesBoth()
    {
        var modules = new AppModuleCollection()
            .Add<RepeatableDiscoverableModule>()
            .AddFromAssembly(new StubAssembly(typeof(RepeatableDiscoverableModule)))
            .Resolve(Builders.Empty());

        modules.Count.ShouldBe(2);
    }

    [Fact]
    public void AddFromAssembly_HonoursRecordedReplace()
    {
        var modules = new AppModuleCollection()
            .Replace<TestDiscoverableModule, ScanReplacementModule>()
            .AddFromAssembly(new StubAssembly(typeof(TestDiscoverableModule)))
            .Resolve(Builders.Empty());

        modules.ShouldHaveSingleItem().ShouldBeOfType<ScanReplacementModule>();
    }
}
