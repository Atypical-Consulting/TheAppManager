using Microsoft.AspNetCore.Authentication.BearerToken;

namespace TheAppManager.Sample.Modules;

/// <summary>Adds bearer-token authentication and authorization.</summary>
public sealed class AuthModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorization();
    }

    public void ConfigureMiddleware(WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }
}
