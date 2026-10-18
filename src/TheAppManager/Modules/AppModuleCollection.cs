using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace TheAppManager.Modules;

/// <summary>
/// A collection of <see cref="IAppModule"/> instances that controls registration and ordering.
/// </summary>
public class AppModuleCollection : IEnumerable<IAppModule>
{
    private readonly List<(IAppModule Module, Func<WebApplicationBuilder, bool>? When)> _modules = [];

    /// <summary>
    /// Adds a module by type. The module must have a parameterless constructor.
    /// </summary>
    /// <typeparam name="TModule">The module type to add.</typeparam>
    /// <returns>This collection for chaining.</returns>
    public AppModuleCollection Add<TModule>() where TModule : IAppModule, new()
    {
        _modules.Add((new TModule(), null));
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
        _modules.Add((module, null));
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
            _modules.Add((new TModule(), null));
        }

        return this;
    }

    /// <summary>
    /// Adds a module by type, applied only when <paramref name="predicate"/> returns true.
    /// The predicate runs once per build, after the builder callbacks, against the real <see cref="WebApplicationBuilder"/>.
    /// </summary>
    /// <typeparam name="TModule">The module type to add.</typeparam>
    /// <param name="predicate">Decides, from the builder (environment, configuration), whether the module is applied.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    public AppModuleCollection AddIf<TModule>(Func<WebApplicationBuilder, bool> predicate) where TModule : IAppModule, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _modules.Add((new TModule(), predicate));
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
            if (_modules.All(m => m.Module.GetType() != module.GetType()))
            {
                _modules.Add((module, null));
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
        var index = _modules.FindIndex(m => m.Module is TOld);
        if (index < 0)
            throw new InvalidOperationException($"No module of type {typeof(TOld).Name} found to replace.");

        _modules[index] = (new TNew(), _modules[index].When);
        return this;
    }

    /// <summary>
    /// Returns the modules in registration order.
    /// </summary>
    internal IReadOnlyList<IAppModule> GetModules()
    {
        return _modules.Select(m => m.Module).ToList();
    }

    /// <summary>
    /// Returns, in registration order, the modules whose predicate is absent or true for <paramref name="builder"/>.
    /// </summary>
    internal IReadOnlyList<IAppModule> GetModules(WebApplicationBuilder builder)
    {
        return _modules.Where(m => m.When is null || m.When(builder)).Select(m => m.Module).ToList();
    }

    /// <inheritdoc />
    public IEnumerator<IAppModule> GetEnumerator() => _modules.Select(m => m.Module).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
