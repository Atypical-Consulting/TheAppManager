using System.Collections;

namespace TheAppManager;

/// <summary>
/// The resolved module plan of a host, registered as a singleton by <c>AddModules</c>.
/// Lists every registered module in registration order, including the ones a predicate skipped.
/// </summary>
public sealed class ModulePlan : IReadOnlyList<ModulePlanEntry>
{
    private readonly List<ModulePlanEntry> _entries;

    internal ModulePlan(IReadOnlyList<(IAppModule Module, bool Applied)> resolved)
    {
        _entries = resolved.Select(e => new ModulePlanEntry(e.Module.GetType(), e.Applied)).ToList();
        Modules = resolved.Where(e => e.Applied).Select(e => e.Module).ToList();
    }

    /// <summary>The applied modules, in registration order.</summary>
    internal IReadOnlyList<IAppModule> Modules { get; }

    internal bool MiddlewareApplied { get; set; }

    internal bool EndpointsMapped { get; set; }

    internal TimeSpan ServicesElapsed { get; set; }

    internal TimeSpan MiddlewareElapsed { get; set; }

    internal TimeSpan EndpointsElapsed { get; set; }

    /// <inheritdoc />
    public int Count => _entries.Count;

    /// <inheritdoc />
    public ModulePlanEntry this[int index] => _entries[index];

    /// <inheritdoc />
    public IEnumerator<ModulePlanEntry> GetEnumerator() => _entries.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// One registration in a <see cref="ModulePlan"/>.
/// </summary>
/// <param name="ModuleType">The module type, after any <c>Replace</c> substitution.</param>
/// <param name="Applied">Whether the module is applied; false when its <c>AddIf</c> predicate returned false.</param>
public sealed record ModulePlanEntry(Type ModuleType, bool Applied);
