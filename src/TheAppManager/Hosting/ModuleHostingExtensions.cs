using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
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
    /// Before resolving, it applies the <see cref="AppModuleAmendments"/> listed in the builder's configuration
    /// (test hosts only); they mutate <paramref name="modules"/>, so tests should prefer the delegate overload.
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

        AppModuleAmendments.Apply(builder.Configuration, modules);
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

    /// <summary>
    /// Runs <see cref="IWebModule.ConfigureMiddleware"/> for each applied <see cref="IWebModule"/> of the plan
    /// registered by <c>AddModules</c>, in order. Other modules are skipped.
    /// </summary>
    /// <param name="app">The built web application.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddModules</c> was not called, or <c>UseModules</c> was already called.</exception>
    public static WebApplication UseModules(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var plan = GetPlan(app.Services, "app.UseModules()");
        if (plan.MiddlewareApplied)
        {
            throw new InvalidOperationException("UseModules can only be called once per application.");
        }

        plan.MiddlewareApplied = true;
        var stopwatch = Stopwatch.StartNew();
        foreach (var module in plan.Modules.OfType<IWebModule>())
        {
            module.ConfigureMiddleware(app);
        }

        plan.MiddlewareElapsed = stopwatch.Elapsed;
        return app;
    }

    /// <summary>
    /// Runs <see cref="IWebModule.ConfigureEndpoints"/> for each applied <see cref="IWebModule"/> of the plan
    /// registered by <c>AddModules</c>, in order, on <paramref name="endpoints"/> — the app itself or a route group.
    /// Other modules are skipped. Endpoints are mapped once per plan, so the routes are never duplicated.
    /// </summary>
    /// <typeparam name="TEndpoints">The route target type, e.g. a <see cref="WebApplication"/> or a <see cref="RouteGroupBuilder"/>.</typeparam>
    /// <param name="endpoints">The route target.</param>
    /// <returns><paramref name="endpoints"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddModules</c> was not called, or <c>MapModules</c> was already called for this plan.</exception>
    public static TEndpoints MapModules<TEndpoints>(this TEndpoints endpoints)
        where TEndpoints : IEndpointRouteBuilder
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var plan = GetPlan(endpoints.ServiceProvider, "MapModules()");
        if (plan.EndpointsMapped)
        {
            throw new InvalidOperationException("MapModules can only be called once per application.");
        }

        plan.EndpointsMapped = true;
        var stopwatch = Stopwatch.StartNew();
        foreach (var module in plan.Modules.OfType<IWebModule>())
        {
            module.ConfigureEndpoints(endpoints);
        }

        plan.EndpointsElapsed = stopwatch.Elapsed;
        return endpoints;
    }

    private static ModulePlan GetPlan(IServiceProvider services, string caller)
        => services.GetService<ModulePlan>()
           ?? throw new InvalidOperationException($"Call builder.AddModules(...) before {caller}.");
}
