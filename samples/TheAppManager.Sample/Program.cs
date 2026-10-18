using TheAppManager;
using TheAppManager.Sample.Modules;

await AppManager.RunAsync(args, modules => modules
    .Add<SwaggerModule>()
    .Add<AuthModule>()
    .Add<WeatherModule>());
