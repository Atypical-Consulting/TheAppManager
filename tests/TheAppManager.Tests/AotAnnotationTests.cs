using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Shouldly;

namespace TheAppManager.Tests;

public class AotAnnotationTests
{
    private static readonly string[] Discovery =
        [nameof(AppModuleCollection.AddFromAssemblyOf), nameof(AppModuleCollection.AddFromAssembly)];

    [Theory]
    [InlineData(nameof(AppModuleCollection.AddFromAssemblyOf))]
    [InlineData(nameof(AppModuleCollection.AddFromAssembly))]
    public void Discovery_methods_require_unreferenced_code(string name) =>
        typeof(AppModuleCollection).GetMethod(name)!
            .GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldNotBeNull();

    [Fact]
    public void Only_discovery_methods_require_unreferenced_code()
    {
        var annotated = typeof(IAppModule).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<RequiresUnreferencedCodeAttribute>() is not null)
            .Select(m => m.Name);

        annotated.ShouldBe(Discovery, ignoreOrder: true);
    }
}
