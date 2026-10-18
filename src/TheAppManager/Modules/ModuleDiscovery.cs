using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TheAppManager;

/// <summary>
/// Discovers <see cref="IAppModule"/> implementations in assemblies via reflection.
/// </summary>
internal static class ModuleDiscovery
{
    internal const string RequiresUnreferencedCodeMessage =
        "Module discovery scans the assembly with reflection; modules only reachable through the scan may be trimmed. " +
        "Register modules with Add<T>() for trimmed or Native AOT apps.";

    /// <summary>
    /// Filters the assembly's types (concrete, closed, reachable, public parameterless constructor,
    /// not <see cref="ExcludeFromDiscoveryAttribute"/>), sorts them by ordinal <see cref="Type.FullName"/>,
    /// then instantiates each survivor exactly once.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    internal static IReadOnlyList<IAppModule> DiscoverModules(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return GetLoadableTypes(assembly)
            .Where(IsDiscoverable)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (IAppModule)Activator.CreateInstance(t)!)
            .ToList();
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
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

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    private static bool IsDiscoverable(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
            && typeof(IAppModule).IsAssignableFrom(type)
            && type.GetCustomAttribute<ExcludeFromDiscoveryAttribute>(inherit: false) is null
            && type.GetConstructor(Type.EmptyTypes) is not null
            && IsReachable(type);
    }

    // Private, protected and private-protected nested types (or types nested in one) are not reachable
    // from the assembly. Internal and protected-internal types are kept.
    private static bool IsReachable(Type type)
    {
        for (var t = type; t is not null; t = t.DeclaringType)
        {
            if (t.IsNestedPrivate || t.IsNestedFamily || t.IsNestedFamANDAssem)
            {
                return false;
            }
        }

        return true;
    }
}
