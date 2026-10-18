var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TheAppManager_Sample>("web");

builder.Build().Run();
