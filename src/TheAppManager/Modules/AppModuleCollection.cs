using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager;

/// <summary>
/// A collection of <see cref="IAppModule"/> instances that controls registration and ordering.
/// </summary>
public sealed class AppModuleCollection : IEnumerable<IAppModule>
{
    private readonly List<(IAppModule Module, Func<IHostApplicationBuilder, bool>? When)> _modules = [];
    private readonly Dictionary<Type, IAppModule> _replacements = [];

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
    /// Adds a module by type, applied only when <paramref name="predicate"/> returns true.
    /// The predicate is evaluated once per <see cref="Resolve"/> call, against the real host builder.
    /// </summary>
    /// <typeparam name="TModule">The module type to add.</typeparam>
    /// <param name="predicate">Decides, from the builder (environment, configuration), whether the module is applied.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    public AppModuleCollection AddIf<TModule>(Func<IHostApplicationBuilder, bool> predicate) where TModule : IAppModule, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _modules.Add((new TModule(), predicate));
        return this;
    }

    /// <summary>
    /// Adds an existing module instance, applied only when <paramref name="predicate"/> returns true.
    /// The predicate is evaluated once per <see cref="Resolve"/> call, against the real host builder.
    /// </summary>
    /// <param name="module">The module to add.</param>
    /// <param name="predicate">Decides, from the builder (environment, configuration), whether the module is applied.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="module"/> or <paramref name="predicate"/> is null.</exception>
    public AppModuleCollection AddIf(IAppModule module, Func<IHostApplicationBuilder, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(predicate);
        _modules.Add((module, predicate));
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
    /// Module types already present in the collection, or used as a <c>Replace</c> replacement, are skipped.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public AppModuleCollection AddFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var module in ModuleDiscovery.DiscoverModules(assembly))
        {
            var type = module.GetType();
            if (_modules.All(m => m.Module.GetType() != type) && _replacements.Values.All(r => r.GetType() != type))
            {
                _modules.Add((module, null));
            }
        }

        return this;
    }

    /// <summary>
    /// Records a persistent substitution: at <see cref="Resolve"/>, every registration whose module type is
    /// exactly <typeparamref name="TOld"/> (subclasses do not match) takes a new <typeparamref name="TNew"/>,
    /// keeping its position and its predicate. It also applies to modules added or scanned after this call.
    /// Calling it again for the same <typeparamref name="TOld"/> overwrites the earlier substitution;
    /// substitutions do not chain.
    /// </summary>
    /// <typeparam name="TOld">The exact module type to replace.</typeparam>
    /// <typeparam name="TNew">The replacement module type.</typeparam>
    /// <returns>This collection for chaining.</returns>
    public AppModuleCollection Replace<TOld, TNew>()
        where TOld : IAppModule
        where TNew : IAppModule, new()
    {
        return Replace<TOld>(new TNew());
    }

    /// <summary>
    /// Records a persistent substitution: at <see cref="Resolve"/>, every registration whose module type is
    /// exactly <typeparamref name="TOld"/> (subclasses do not match) takes <paramref name="replacement"/>,
    /// keeping its position and its predicate. It also applies to modules added or scanned after this call.
    /// Calling it again for the same <typeparamref name="TOld"/> overwrites the earlier substitution;
    /// substitutions do not chain. The same instance is shared across <see cref="Resolve"/> calls.
    /// </summary>
    /// <typeparam name="TOld">The exact module type to replace.</typeparam>
    /// <param name="replacement">The replacement module.</param>
    /// <returns>This collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="replacement"/> is null.</exception>
    public AppModuleCollection Replace<TOld>(IAppModule replacement) where TOld : IAppModule
    {
        ArgumentNullException.ThrowIfNull(replacement);
        _replacements[typeof(TOld)] = replacement;
        return this;
    }

    /// <summary>
    /// Resolves the effective plan: substitutions applied, then the modules whose predicate is absent or true for
    /// <paramref name="builder"/>, in registration order. Each predicate is evaluated exactly once per call.
    /// </summary>
    /// <param name="builder">The host builder the predicates are evaluated against.</param>
    /// <returns>The applied modules, in registration order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a <c>Replace</c> names a module type that no registration has, or when a module type
    /// (after substitution, predicates included) is registered more than once and its class does not carry
    /// <see cref="AllowMultipleInstancesAttribute"/>.
    /// </exception>
    public IReadOnlyList<IAppModule> Resolve(IHostApplicationBuilder builder)
    {
        return ResolveEntries(builder).Where(e => e.Applied).Select(e => e.Module).ToList();
    }

    /// <summary>
    /// Resolves every registration, in order, with whether it is applied for <paramref name="builder"/>.
    /// </summary>
    internal IReadOnlyList<(IAppModule Module, bool Applied)> ResolveEntries(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var entries = _modules
            .Select(m => (Module: _replacements.GetValueOrDefault(m.Module.GetType(), m.Module), m.When))
            .ToList();

        var missing = _replacements.Keys.FirstOrDefault(old => _modules.All(m => m.Module.GetType() != old));
        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"Cannot replace module type '{missing.FullName}': no module of that type is registered.");
        }

        // Identity is checked over every registration, applied or not, so the error does not depend on the environment.
        var duplicate = entries
            .GroupBy(m => m.Module.GetType())
            .FirstOrDefault(g => g.Count() > 1 && !g.Key.IsDefined(typeof(AllowMultipleInstancesAttribute), inherit: false));
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Module type '{duplicate.Key.FullName}' is registered more than once. " +
                "Register it once, or mark the class with [AllowMultipleInstances].");
        }

        return entries.Select(m => (m.Module, m.When is null || m.When(builder))).ToList();
    }

    /// <summary>
    /// Enumerates the registrations as declared — original instances, predicates not evaluated.
    /// Use <see cref="Resolve"/> to get the effective plan.
    /// </summary>
    public IEnumerator<IAppModule> GetEnumerator() => _modules.Select(m => m.Module).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
