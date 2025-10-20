using OrleansVoting.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var redisConnectionName = builder.Configuration.GetConnectionString("voting-redis")
    ?? throw new InvalidOperationException("Redis connection string is required");

// Configure as Orleans client (no grain hosting)
builder.UseOrleansClient(client =>
{
    // Must match the cluster configuration from AppHost
    client.Configure<Orleans.Configuration.ClusterOptions>(options =>
    {
        options.ClusterId = "voting-cluster";
        options.ServiceId = "voting-cluster";
    });
    
    // Only configure clustering for client - no storage needed
    client.UseRedisClustering(redisConnectionName);
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
