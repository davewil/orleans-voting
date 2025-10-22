using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using OrleansVoting.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace OrleansVoting.Tests.Api;

/// <summary>
/// Startup filter that adds middleware to set a fixed IP address for testing.
/// This allows each test to have a unique client ID without modifying production code.
/// </summary>
public class TestIpAddressStartupFilter : IStartupFilter
{
    private readonly string _ipAddress;

    public TestIpAddressStartupFilter(string ipAddress)
    {
        _ipAddress = ipAddress;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, next) =>
            {
                // Set RemoteIpAddress to test IP for isolation
                context.Connection.RemoteIpAddress = IPAddress.Parse(_ipAddress);
                await next();
            });

            next(app);
        };
    }
}

public class PollsApiTests : IClassFixture<TestClusterFixture>, IAsyncLifetime
{
    private readonly TestCluster _cluster;
    private readonly string _testClientId;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public PollsApiTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
        // Each test instance gets a unique IP address to prevent throttling conflicts
        // Use 127.0.0.x where x is a random number to ensure uniqueness
        var random = new Random().Next(1, 255);
        _testClientId = $"127.0.0.{random}";
    }

    public async Task InitializeAsync()
    {
        // Create WebApplicationFactory that uses the test cluster
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Set configuration values before app builds
                builder.UseSetting("ConnectionStrings:voting-redis", "localhost:6379");
                builder.UseSetting("Orleans:ClusterOptions:ClusterId", "test-cluster");
                builder.UseSetting("Orleans:ClusterOptions:ServiceId", "test-app");

                // Override Orleans client with test cluster client
                builder.ConfigureTestServices(services =>
                {
                    // Add startup filter to inject middleware that sets unique IP address
                    services.AddSingleton<IStartupFilter>(new TestIpAddressStartupFilter(_testClientId));
                    // Remove ALL IHostedService registrations to prevent Orleans client from starting
                    var hostedServices = services
                        .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                        .ToList();

                    foreach (var hostedService in hostedServices)
                    {
                        services.Remove(hostedService);
                    }

                    // Remove the IGrainFactory and IClusterClient registrations
                    var grainFactoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGrainFactory));
                    if (grainFactoryDescriptor != null)
                    {
                        services.Remove(grainFactoryDescriptor);
                    }

                    var clusterClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IClusterClient));
                    if (clusterClientDescriptor != null)
                    {
                        services.Remove(clusterClientDescriptor);
                    }

                    // Add test cluster client as both IGrainFactory and IClusterClient
                    services.AddSingleton<IGrainFactory>(_cluster.Client);
                    services.AddSingleton<IClusterClient>(_cluster.Client);
                });
            });

        _client = _factory.CreateClient();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task POST_Polls_CreatesPoll_ReturnsId()
    {
        // Arrange
        var request = new
        {
            Question = "Favorite color?",
            Options = new[] { "Red", "Blue", "Green" }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/polls", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CreatePollResponse>();
        result!.PollId.Should().NotBeNullOrEmpty();
        result.PollId.Length.Should().Be(6);
    }

    [Fact]
    public async Task GET_Polls_Id_ReturnsPollResults()
    {
        // Arrange - Create a poll first
        var createRequest = new
        {
            Question = "Test?",
            Options = new[] { "A", "B" }
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/polls", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatePollResponse>();

        // Act - Get the poll
        var response = await _client!.GetAsync($"/api/polls/{created!.PollId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var poll = await response.Content.ReadFromJsonAsync<PollResultsResponse>();
        poll!.Question.Should().Be("Test?");
        poll.Options.Should().HaveCount(2);
        poll.Voted.Should().BeFalse();
    }

    [Fact]
    public async Task POST_Polls_Id_Vote_RecordsVote()
    {
        // Arrange - Create poll
        var createRequest = new
        {
            Question = "Test?",
            Options = new[] { "A", "B" }
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/polls", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatePollResponse>();

        // Act - Vote
        var voteRequest = new { OptionIndex = 0 };
        var voteResponse = await _client!.PostAsJsonAsync(
            $"/api/polls/{created!.PollId}/vote",
            voteRequest);

        // Assert
        voteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await voteResponse.Content.ReadFromJsonAsync<PollResultsResponse>();
        result!.Options[0].Votes.Should().Be(1);
        result.Voted.Should().BeTrue();
    }

    [Fact]
    public async Task POST_Polls_Id_Vote_DoubleVote_Returns400()
    {
        // Arrange - Create poll and vote once
        var createRequest = new
        {
            Question = "Test?",
            Options = new[] { "A" }
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/polls", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatePollResponse>();
        var voteRequest = new { OptionIndex = 0 };
        await _client!.PostAsJsonAsync($"/api/polls/{created!.PollId}/vote", voteRequest);

        // Act - Try to vote again
        var response = await _client!.PostAsJsonAsync(
            $"/api/polls/{created.PollId}/vote",
            voteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Polls_Throttling_Returns429()
    {
        // Arrange - Make 10 requests (at threshold)
        for (int i = 0; i < 10; i++)
        {
            var request = new
            {
                Question = $"Poll {i}?",
                Options = new[] { "A" }
            };
            await _client!.PostAsJsonAsync("/api/polls", request);
        }

        // Act - 11th request should be throttled
        var finalRequest = new
        {
            Question = "Poll 11?",
            Options = new[] { "A" }
        };
        var response = await _client!.PostAsJsonAsync("/api/polls", finalRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}

// DTOs for API responses
public record CreatePollResponse(string PollId);
public record PollResultsResponse(
    string Question,
    List<OptionDto> Options,
    bool Voted);
public record OptionDto(string Text, int Votes);
