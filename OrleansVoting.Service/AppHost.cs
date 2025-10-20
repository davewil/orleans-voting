using OrleansVoting.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddKeyedRedisClient("voting-redis");

// Configure as Orleans client (no grain hosting)
builder.UseOrleansClient(client =>
{
    // Clustering configuration will come from Aspire via .WithReference(orleans)
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
