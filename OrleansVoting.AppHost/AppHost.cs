var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("voting-redis");

// Shared Orleans cluster identity
const string clusterId = "voting-cluster";
const string serviceId = "voting-app";

// Dedicated silo instances (grain hosting)
var silo = builder.AddProject<Projects.OrleansVoting_Silo>("voting-silo")
    .WithReference(redis)
    .WithEnvironment("Orleans__ClusterOptions__ClusterId", clusterId)
    .WithEnvironment("Orleans__ClusterOptions__ServiceId", serviceId);

// Web frontend (Orleans client only)
builder.AddProject<Projects.OrleansVoting_WebApp>("voting-web")
    .WithReference(redis)
    .WithEnvironment("Orleans__ClusterOptions__ClusterId", clusterId)
    .WithEnvironment("Orleans__ClusterOptions__ServiceId", serviceId)
    .WaitFor(silo)
    .WithExternalHttpEndpoints();

builder.Build().Run();
