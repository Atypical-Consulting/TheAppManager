using TheAppManager;

namespace TheAppManager.Sample.Modules;

/// <summary>
/// Adds Swagger/OpenAPI support to the application.
/// </summary>
public class SwaggerModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    public void ConfigureMiddleware(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
    }
}
