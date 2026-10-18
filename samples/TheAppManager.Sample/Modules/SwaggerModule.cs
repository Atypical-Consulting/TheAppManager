namespace TheAppManager.Sample.Modules;

/// <summary>Adds Swagger/OpenAPI support (registered with AddIf in Development only).</summary>
public sealed class SwaggerModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    public void ConfigureMiddleware(WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}
