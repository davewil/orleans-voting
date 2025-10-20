var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKeyedRedisClient("voting-redis");

builder.UseOrleans();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
