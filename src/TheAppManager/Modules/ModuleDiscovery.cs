using System.Reflection;

namespace TheAppManager;

/// <summary>
/// Discovers <see cref="IAppModule"/> implementations in assemblies via reflection.
/// </summary>
public static class ModuleDiscovery
{
    /// <summary>
    /// Scans the specified assembly for concrete types implementing <see cref="IAppModule"/>
    /// with a public parameterless constructor, and returns instances of each.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>A collection of discovered module instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public static IEnumerable<IAppModule> DiscoverModules(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return GetLoadableTypes(assembly)
            .Where(IsAppModule)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => (IAppModule)Activator.CreateInstance(t)!);
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>().ToArray();
        }
    }

    private static bool IsAppModule(Type type)
    {
        return type is { IsAbstract: false, IsInterface: false }
            && typeof(IAppModule).IsAssignableFrom(type)
            && type.GetConstructor(Type.EmptyTypes) is not null;
    }
}
