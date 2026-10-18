using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace TheAppManager.Sample.Modules;

/// <summary>Development-only login endpoint that mints a bearer token for any user name.</summary>
public sealed class DevLoginModule : IWebModule
{
    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/login", (string user) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, user)], BearerTokenDefaults.AuthenticationScheme));
                return TypedResults.SignIn(principal, authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
            })
            .AllowAnonymous();
    }
}
