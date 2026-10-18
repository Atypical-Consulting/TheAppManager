using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager;

/// <summary>
/// Applies an <see cref="AppModuleCollection"/> to any host: <c>AddModules</c> on the builder,
/// then, for a web app, <c>UseModules</c> and <c>MapModules</c>.
/// </summary>
public static class ModuleHostingExtensions
{
    /// <summary>
    /// Creates a module collection, lets <paramref name="configure"/> fill it, and applies it to
    /// <paramref name="builder"/>. See <see cref="AddModules{TBuilder}(TBuilder, AppModuleCollection)"/>.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type (web, worker or console).</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="configure">Registers the modules.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddModules</c> was already called on this builder, or the plan is invalid.</exception>
    public static TBuilder AddModules<TBuilder>(this TBuilder builder, Action<AppModuleCollection> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var modules = new AppModuleCollection();
        configure(modules);
        return builder.AddModules(modules);
    }

    /// <summary>
    /// Resolves <paramref name="modules"/> once against <paramref name="builder"/>, registers the resulting
    /// <see cref="ModulePlan"/> as a singleton (logged at Information when the host starts), and runs
    /// <see cref="IAppModule.ConfigureServices"/> for each applied module, in order.
    /// Predicates see the builder as it is when this method is called; later builder changes are not visible to them.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type (web, worker or console).</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="modules">The modules to apply. The same collection may be applied to several builders.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="modules"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddModules</c> was already called on this builder, or the plan is invalid.</exception>
    public static TBuilder AddModules<TBuilder>(this TBuilder builder, AppModuleCollection modules)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(modules);

        if (builder.Services.Any(d => d.ServiceType == typeof(ModulePlan)))
        {
            throw new InvalidOperationException(
                "AddModules can only be called once per builder; compose every module in one AppModuleCollection.");
        }

        var plan = new ModulePlan(modules.ResolveEntries(builder));
        builder.Services.AddSingleton(plan);
        builder.Services.AddHostedService<ModulePlanLogger>();

        var stopwatch = Stopwatch.StartNew();
        foreach (var module in plan.Modules)
        {
            module.ConfigureServices(builder);
        }

        plan.ServicesElapsed = stopwatch.Elapsed;
        return builder;
    }
}
