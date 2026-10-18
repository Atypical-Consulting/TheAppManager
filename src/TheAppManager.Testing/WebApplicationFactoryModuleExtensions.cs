using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TheAppManager.Testing;

/// <summary>
/// Replaces or removes modules of the real app under <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// </summary>
public static class WebApplicationFactoryModuleExtensions
{
    /// <summary>
    /// Returns a factory whose host applies <paramref name="amend"/> to the app's module composition,
    /// after <c>Program</c>'s own <c>AddModules</c> configuration and before the plan is resolved.
    /// <c>Program</c> must pass <c>args</c> to <c>WebApplication.CreateBuilder(args)</c>; otherwise building
    /// the host throws instead of silently testing the real modules.
    /// </summary>
    /// <typeparam name="TProgram">The app's entry point type.</typeparam>
    /// <param name="factory">The factory to derive from.</param>
    /// <param name="amend">Edits the composition, e.g. <c>m =&gt; m.Replace&lt;Real, Fake&gt;()</c> or <c>m =&gt; m.Without&lt;T&gt;()</c>.</param>
    /// <returns>A new factory; chained calls apply in call order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> or <paramref name="amend"/> is null.</exception>
    public static WebApplicationFactory<TProgram> WithModules<TProgram>(
        this WebApplicationFactory<TProgram> factory, Action<AppModuleCollection> amend)
        where TProgram : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(amend);

        // Runs once per host build, before Program's entry point: each build gets its own id.
        return factory.WithWebHostBuilder(builder =>
        {
            var applied = false;
            var id = AppModuleAmendments.Register(modules =>
            {
                applied = true;
                amend(modules);
            });

            // UseSetting reaches CreateBuilder(args) as a command-line argument.
            builder.UseSetting($"{AppModuleAmendments.ConfigurationSection}:{id}", "true");

            // ConfigureServices callbacks run after Program's pre-Build code, i.e. after AddModules.
            builder.ConfigureServices(_ =>
            {
                if (!applied)
                {
                    throw new InvalidOperationException(
                        "The module amendment passed to WithModules was not applied. Program must pass args to " +
                        "WebApplication.CreateBuilder(args) and call AddModules.");
                }
            });
        });
    }

    /// <summary>Removes <typeparamref name="TModule"/> from the composition (replaces it by a no-op placeholder).</summary>
    /// <typeparam name="TModule">The exact module type to remove.</typeparam>
    /// <param name="modules">The module collection.</param>
    /// <returns><paramref name="modules"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modules"/> is null.</exception>
    public static AppModuleCollection Without<TModule>(this AppModuleCollection modules)
        where TModule : IAppModule
    {
        ArgumentNullException.ThrowIfNull(modules);
        return modules.Replace<TModule>(new RemovedModule<TModule>());
    }

    // Each closed generic is its own type, so removing several modules never trips the duplicate rule.
    private sealed class RemovedModule<TModule> : IWebModule;
}
