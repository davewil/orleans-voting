using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansVoting.Api;
using OrleansVoting.Contracts.Grains;
using OrleansVoting.Contracts.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure as Orleans client (same as WebApp)
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register client ID provider for request identification
builder.Services.AddSingleton<IClientIdProvider, IpAddressClientIdProvider>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Minimal API endpoints
var polls = app.MapGroup("/api/polls");

polls.MapPost("/", async (
    [FromBody] CreatePollRequest request,
    [FromServices] IGrainFactory grainFactory,
    [FromServices] IClientIdProvider clientIdProvider,
    HttpContext context) =>
{
    try
    {
        var clientId = clientIdProvider.GetClientId(context);
        var userGrain = grainFactory.GetGrain<IUserAgentGrain>(clientId);

        var pollId = await userGrain.CreatePoll(new PollState
        {
            Question = request.Question,
            Options = request.Options.Select(o => (o, 0)).ToList()
        });

        return Results.Ok(new CreatePollResponse(pollId));
    }
    catch (ThrottlingException)
    {
        return Results.StatusCode(429); // Too Many Requests
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

polls.MapGet("/{pollId}", async (
    string pollId,
    [FromServices] IGrainFactory grainFactory,
    [FromServices] IClientIdProvider clientIdProvider,
    HttpContext context) =>
{
    var clientId = clientIdProvider.GetClientId(context);
    var userGrain = grainFactory.GetGrain<IUserAgentGrain>(clientId);

    var (results, voted) = await userGrain.GetPollResults(pollId);

    return Results.Ok(new PollResultsResponse(
        results.Question,
        results.Options.Select(o => new OptionDto(o.Item1, o.Item2)).ToList(),
        voted
    ));
});

polls.MapPost("/{pollId}/vote", async (
    string pollId,
    [FromBody] VoteRequest request,
    [FromServices] IGrainFactory grainFactory,
    [FromServices] IClientIdProvider clientIdProvider,
    HttpContext context) =>
{
    try
    {
        var clientId = clientIdProvider.GetClientId(context);
        var userGrain = grainFactory.GetGrain<IUserAgentGrain>(clientId);

        var result = await userGrain.AddVote(pollId, request.OptionIndex);

        return Results.Ok(new PollResultsResponse(
            result.Question,
            result.Options.Select(o => new OptionDto(o.Item1, o.Item2)).ToList(),
            true
        ));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

// DTOs
public record CreatePollRequest(string Question, List<string> Options);
public record CreatePollResponse(string PollId);
public record PollResultsResponse(string Question, List<OptionDto> Options, bool Voted);
public record OptionDto(string Text, int Votes);
public record VoteRequest(int OptionIndex);

// Required for WebApplicationFactory in tests
public partial class Program { }
