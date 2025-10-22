using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansVoting.Grains;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKeyedRedisClient("voting-redis");

var redisConn = builder.Configuration.GetConnectionString("voting-redis")
	?? throw new InvalidOperationException("Redis connection string is required");
var redisOptions = ConfigurationOptions.Parse(redisConn);

var clusterId = builder.Configuration["Orleans:ClusterOptions:ClusterId"] ?? "voting-cluster";
var serviceId = builder.Configuration["Orleans:ClusterOptions:ServiceId"] ?? "voting-app";

builder.UseOrleans(silo =>
{
	// Cluster identity
	silo.Configure<ClusterOptions>(o =>
	{
		o.ClusterId = clusterId;
		o.ServiceId = serviceId;
	});

	// Redis clustering
	silo.UseRedisClustering(o =>
	{
		o.ConfigurationOptions = redisOptions;
	});

	// Redis grain storage for "votes"
	silo.AddRedisGrainStorage("votes", o =>
	{
		o.ConfigurationOptions = redisOptions;
	});

});

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
