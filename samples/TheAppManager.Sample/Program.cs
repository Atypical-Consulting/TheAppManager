using TheAppManager;
using TheAppManager.Sample.Modules;

// Explicit module registration — full control over which modules run
AppManager.Start(args, modules =>
{
    modules
        .Add<SwaggerModule>()
        .Add<AuthModule>()
        .Add<WeatherModule>();
});

// Alternative: auto-discover all IAppModule implementations in this assembly
// AppManager.Start(args);
