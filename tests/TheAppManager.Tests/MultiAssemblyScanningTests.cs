using System.Reflection;
using TheAppManager.Modules;

namespace TheAppManager.Tests;

/// <summary>
/// A simple discoverable module used by multi-assembly scanning tests.
/// </summary>
public class TestDiscoverableModule : IAppModule;

public class MultiAssemblyScanningTests
{
    [Fact]
    public void AddFromAssemblyOf_SkipsModuleAlreadyAdded()
    {
        var collection = new AppModuleCollection();

        collection.Add<TestDiscoverableModule>().AddFromAssemblyOf<TestDiscoverableModule>();

        collection.Count(m => m.GetType() == typeof(TestDiscoverableModule)).ShouldBe(1);
    }

    [Fact]
    public void AddFromAssemblyOf_DiscoversModulesInAssembly()
    {
        // Arrange
        var collection = new AppModuleCollection();

        // Act
        collection.AddFromAssemblyOf<TestDiscoverableModule>();

        // Assert
        collection.ShouldContain(m => m.GetType() == typeof(TestDiscoverableModule));
    }

    [Fact]
    public void AddFromAssembly_DiscoversModules()
    {
        // Arrange
        var collection = new AppModuleCollection();

        // Act
        collection.AddFromAssembly(Assembly.GetExecutingAssembly());

        // Assert
        collection.ShouldContain(m => m.GetType() == typeof(TestDiscoverableModule));
    }

    [Fact]
    public void AddFromAssembly_ThrowsOnNull()
    {
        // Arrange
        var collection = new AppModuleCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => collection.AddFromAssembly(null!));
    }

    [Fact]
    public void AddFromAssemblyOf_ReturnsSelfForChaining()
    {
        // Arrange
        var collection = new AppModuleCollection();

        // Act
        var result = collection.AddFromAssemblyOf<TestDiscoverableModule>();

        // Assert
        result.ShouldBeSameAs(collection);
    }
}
