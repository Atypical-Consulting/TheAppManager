using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace TheAppManager;

/// <summary>
/// An <see cref="IAppModule"/> for an ASP.NET Core web application: it also configures the middleware
/// pipeline and the endpoint routes. The web phases run only for modules implementing this interface.
/// All methods have default no-op implementations — only override what you need.
/// </summary>
public interface IWebModule : IAppModule
{
    /// <summary>
    /// Configures the middleware pipeline.
    /// </summary>
    /// <param name="app">The web application to configure middleware on.</param>
    void ConfigureMiddleware(WebApplication app)
    {
    }

    /// <summary>
    /// Configures the endpoint routes.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
