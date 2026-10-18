using TheAppManager;

namespace TheAppManager.Sample.Modules;

/// <summary>
/// Adds authentication middleware to the application.
/// </summary>
public class AuthModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddAuthentication();
    }

    public void ConfigureMiddleware(WebApplication app)
    {
        app.UseAuthentication();
    }
}
