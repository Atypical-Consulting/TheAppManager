using System.Collections;
using System.Reflection;

namespace TheAppManager.Modules;

/// <summary>
/// A collection of <see cref="IAppModule"/> instances that controls registration and ordering.
/// </summary>
public class AppModuleCollection : IEnumerable<IAppModule>
{
    private readonly List<IAppModule> _modules = [];

    /// <summary>
    /// Adds a module by type. The module must have a parameterless constructor.
    /// </summary>
    /// <typeparam name="TModule">The module type to add.</typeparam>
    /// <returns>This collection for chaining.</returns>
    public AppModuleCollection Add<TModule>() where TModule : IAppModule, new()
    {
        _modules.Add(new TModule());
        return this;
    }

    /// <summary>
    /// Adds an existing module instance.
    /// </summary>
    /// <param name="module">The module to add.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="module"/> is null.</exception>
    public AppModuleCollection Add(IAppModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _modules.Add(module);
        return this;
    }

    /// <summary>
    /// Conditionally adds a module by type. The module is only added when <paramref name="condition"/> is true.
    /// </summary>
    /// <typeparam name="TModule">The module type to add.</typeparam>
    /// <param name="condition">When true, the module is added; otherwise it is skipped.</param>
    /// <returns>This collection for chaining.</returns>
    public AppModuleCollection AddIf<TModule>(bool condition) where TModule : IAppModule, new()
    {
        if (condition)
        {
            _modules.Add(new TModule());
        }

        return this;
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TModule"/> for all
    /// <see cref="IAppModule"/> implementations and adds them to this collection.
    /// </summary>
    /// <typeparam name="TModule">A type whose assembly will be scanned.</typeparam>
    /// <returns>This collection for chaining.</returns>
    public AppModuleCollection AddFromAssemblyOf<TModule>() where TModule : IAppModule
    {
        return AddFromAssembly(typeof(TModule).Assembly);
    }

    /// <summary>
    /// Scans the specified <paramref name="assembly"/> for all <see cref="IAppModule"/>
    /// implementations and adds them to this collection.
    /// Module types already present in the collection are skipped.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public AppModuleCollection AddFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var module in ModuleDiscovery.DiscoverModules(assembly))
        {
            if (_modules.All(m => m.GetType() != module.GetType()))
            {
                _modules.Add(module);
            }
        }

        return this;
    }

    /// <summary>
    /// Replaces the first module of type <typeparamref name="TOld"/> with a new instance of <typeparamref name="TNew"/>.
    /// The replacement occupies the same position in the registration order.
    /// If no module of type <typeparamref name="TOld"/> is found, throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <typeparam name="TOld">The module type to replace.</typeparam>
    /// <typeparam name="TNew">The replacement module type.</typeparam>
    /// <returns>This collection for chaining.</returns>
    public AppModuleCollection Replace<TOld, TNew>()
        where TOld : IAppModule
        where TNew : IAppModule, new()
    {
        var index = _modules.FindIndex(m => m is TOld);
        if (index < 0)
            throw new InvalidOperationException($"No module of type {typeof(TOld).Name} found to replace.");

        _modules[index] = new TNew();
        return this;
    }

    /// <summary>
    /// Returns the modules in registration order.
    /// </summary>
    internal IReadOnlyList<IAppModule> GetModules()
    {
        return _modules;
    }

    /// <inheritdoc />
    public IEnumerator<IAppModule> GetEnumerator() => _modules.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
