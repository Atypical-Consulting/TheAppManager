using TheAppManager.Sample.Modules;

namespace TheAppManager.Sample;

/// <summary>The named module composition, shared by Program.cs and the tests.</summary>
public static class AppModules
{
    public static void Configure(AppModuleCollection modules) => modules
        .Add<ErrorHandlingModule>()
        .Add<AuthModule>()
        .AddIf<SwaggerModule>(b => b.Environment.IsDevelopment())
        .AddIf<DevLoginModule>(b => b.Environment.IsDevelopment())
        .Add<WeatherModule>();
}
