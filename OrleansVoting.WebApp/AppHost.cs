using Microsoft.Extensions.Hosting;
using OrleansVoting.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKeyedRedisClient("voting-redis");

// Configure as Orleans client (no grain hosting)
// Uses ServiceDefaults extension method which automatically adds ActivityPropagation for telemetry
builder.ConfigureOrleansClient(client =>
{
	var clusterId = builder.Configuration["Orleans:ClusterOptions:ClusterId"] ?? "voting-cluster";
	var serviceId = builder.Configuration["Orleans:ClusterOptions:ServiceId"] ?? "voting-app";
	var redisConn = builder.Configuration.GetConnectionString("voting-redis")
		?? throw new InvalidOperationException("Redis connection string is required");

	client.Configure<Orleans.Configuration.ClusterOptions>(o =>
	{
		o.ClusterId = clusterId;
		o.ServiceId = serviceId;
	});

	client.UseRedisClustering(o =>
	{
		o.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConn);
	});
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<PollService>();
builder.Services.AddScoped<DemoService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapDefaultEndpoints();

app.Run();
