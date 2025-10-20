var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("voting-redis");

var orleans = builder.AddOrleans("voting-cluster")
    .WithClustering(redis)
    .WithGrainStorage("votes", redis);

// NEW: Dedicated silo instances
var silo = builder.AddProject<Projects.OrleansVoting_Silo>("voting-silo")
    .WithReference(orleans)
    .WithReplicas(2);  // Start with 2 replicas

// EXISTING: Service with embedded silo (still running!)
builder.AddProject<Projects.OrleansVoting_Service>("voting-fe")
    .WithReference(orleans)
    .WaitFor(redis)
    .WithReplicas(3)
    .WithExternalHttpEndpoints();

builder.Build().Run();
