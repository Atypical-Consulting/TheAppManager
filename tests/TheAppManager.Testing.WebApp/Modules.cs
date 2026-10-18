namespace TheAppManager.Testing.WebApp;

/// <summary>Maps <c>GET /ping</c> to <c>"real"</c>.</summary>
public sealed class PingModule : IWebModule
{
    /// <inheritdoc />
    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/ping", () => "real");
}

/// <summary>Maps <c>GET /extra</c>.</summary>
public sealed class ExtraModule : IWebModule
{
    /// <inheritdoc />
    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/extra", () => "extra");
}
