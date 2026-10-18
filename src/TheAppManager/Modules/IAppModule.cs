using Microsoft.Extensions.Hosting;

namespace TheAppManager;

/// <summary>
/// Defines a composable, host-agnostic module: it configures services and configuration on any
/// <see cref="IHostApplicationBuilder"/> (web, worker or console host).
/// Implement <see cref="IWebModule"/> instead when the module also configures middleware or endpoints.
/// The method has a default no-op implementation — only override what you need.
/// Modules are applied in the order they are registered.
/// </summary>
public interface IAppModule
{
    /// <summary>
    /// Configures services in the dependency injection container.
    /// </summary>
    /// <param name="builder">The host application builder, providing access to services, configuration, and environment.</param>
    void ConfigureServices(IHostApplicationBuilder builder)
    {
    }
}
