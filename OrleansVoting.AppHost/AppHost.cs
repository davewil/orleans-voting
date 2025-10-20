var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("voting-redis");

var orleans = builder.AddOrleans("voting-cluster")
    .WithClustering(redis)
    .WithGrainStorage("votes", redis);

// Dedicated silo instances (grain hosting)
var silo = builder.AddProject<Projects.OrleansVoting_Silo>("voting-silo")
    .WithReference(orleans)
    .WithReplicas(3);

// Web frontend (original state hosted Orleans too — but we are reverting to start of Phase 5 which still had Orleans here)
builder.AddProject<Projects.OrleansVoting_Service>("voting-fe")
    .WithReference(orleans)
    .WaitFor(redis)
    .WithReplicas(3)
    .WithExternalHttpEndpoints();

builder.Build().Run();
