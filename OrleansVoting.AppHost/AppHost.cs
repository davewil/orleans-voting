var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("voting-redis");

var orleans = builder.AddOrleans("voting-cluster")
    .WithClustering(redis)
    .WithGrainStorage("votes", redis);

// Dedicated silo instances (grain hosting)
var silo = builder.AddProject<Projects.OrleansVoting_Silo>("voting-silo")
    .WithReference(orleans)
    .WithReplicas(3);

// Web frontend (Orleans client only)
builder.AddProject<Projects.OrleansVoting_Service>("voting-fe")
    .WithReference(redis)  // Only needs Redis for client clustering
    .WaitFor(silo)  // Must wait for at least one silo
    .WithReplicas(3)
    .WithExternalHttpEndpoints();

builder.Build().Run();
