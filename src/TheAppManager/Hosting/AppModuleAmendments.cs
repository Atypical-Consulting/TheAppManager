using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace TheAppManager;

/// <summary>
/// Process-wide amendments that <c>AddModules</c> applies to the composition of a host it did not build
/// (used by TheAppManager.Testing under <c>WebApplicationFactory</c>). Not meant for production code.
/// </summary>
/// <remarks>
/// A host opts in through configuration: every child key of <see cref="ConfigurationSection"/> is an id returned by
/// <see cref="Register"/>. <c>AddModules</c> applies those amendments in ascending id order, after the app's own
/// composition and before the plan is resolved, and consumes each id once.
/// </remarks>
public static class AppModuleAmendments
{
    /// <summary>Configuration section whose child keys are amendment ids, e.g. <c>--TheAppManager:Amendments:7=true</c>.</summary>
    public const string ConfigurationSection = "TheAppManager:Amendments";

    private static readonly ConcurrentDictionary<string, Action<AppModuleCollection>> Pending = new();
    private static long _lastId;

    /// <summary>Registers <paramref name="amend"/> and returns its id (a positive integer, increasing per call).</summary>
    /// <param name="amend">The amendment applied to the module collection.</param>
    /// <returns>The id to list under <see cref="ConfigurationSection"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="amend"/> is null.</exception>
    public static string Register(Action<AppModuleCollection> amend)
    {
        ArgumentNullException.ThrowIfNull(amend);

        // ponytail: an id whose host never reaches AddModules stays registered (one delegate leaked per failed build).
        var id = Interlocked.Increment(ref _lastId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Pending[id] = amend;
        return id;
    }

    /// <summary>Consumes and applies, in ascending id order, every amendment listed in <paramref name="configuration"/>.</summary>
    internal static void Apply(IConfiguration configuration, AppModuleCollection modules)
    {
        // GetChildren sorts numeric keys numerically, so ids apply in registration order.
        foreach (var child in configuration.GetSection(ConfigurationSection).GetChildren())
        {
            if (!Pending.TryRemove(child.Key, out var amend))
            {
                throw new InvalidOperationException(
                    $"Module amendment '{child.Key}' is not registered or was already applied; " +
                    "each AppModuleAmendments id applies to exactly one host.");
            }

            amend(modules);
        }
    }
}
