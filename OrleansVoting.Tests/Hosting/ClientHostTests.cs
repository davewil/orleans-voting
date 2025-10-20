using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;
using Xunit;
using OrleansVoting.Contracts.Grains;

namespace OrleansVoting.Tests.Hosting;

public class ClientHostTests
{
    [Fact]
    public async Task Client_CanConnect_ToSilo()
    {
        // Arrange - Start a silo
        var siloBuilder = Host.CreateApplicationBuilder();
        siloBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
        siloBuilder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.AddMemoryGrainStorage("votes");
        });
        var siloHost = siloBuilder.Build();
        await siloHost.StartAsync();

        // Arrange - Start a client
        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
        clientBuilder.UseOrleansClient(client =>
        {
            client.UseLocalhostClustering();
        });
        var clientHost = clientBuilder.Build();
        await clientHost.StartAsync();

        // Act
        var grainFactory = clientHost.Services.GetRequiredService<IGrainFactory>();
        var grain = grainFactory.GetGrain<IPollGrain>("test-client-grain");
        var state = new PollState 
        { 
            Question = "Test?", 
            Options = new List<(string, int)>() 
        };
        await grain.CreatePoll(state);
        var result = await grain.GetCurrentResults();

        // Assert
        result.Question.Should().Be("Test?");

        // Cleanup
        await clientHost.StopAsync();
        clientHost.Dispose();
        await siloHost.StopAsync();
        siloHost.Dispose();
    }

    [Fact]
    public async Task Client_CanCallMultipleGrains_OnSilo()
    {
        // Arrange - Start a silo
        var siloBuilder = Host.CreateApplicationBuilder();
        siloBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
        siloBuilder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.AddMemoryGrainStorage("votes");
        });
        var siloHost = siloBuilder.Build();
        await siloHost.StartAsync();

        // Arrange - Start a client
        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
        clientBuilder.UseOrleansClient(client =>
        {
            client.UseLocalhostClustering();
        });
        var clientHost = clientBuilder.Build();
        await clientHost.StartAsync();

        // Act - Call multiple different grains
        var grainFactory = clientHost.Services.GetRequiredService<IGrainFactory>();
        
        var grain1 = grainFactory.GetGrain<IPollGrain>("poll-1");
        var state1 = new PollState 
        { 
            Question = "Poll 1?", 
            Options = new List<(string, int)> { ("A", 0) } 
        };
        await grain1.CreatePoll(state1);
        
        var grain2 = grainFactory.GetGrain<IPollGrain>("poll-2");
        var state2 = new PollState 
        { 
            Question = "Poll 2?", 
            Options = new List<(string, int)> { ("B", 0) } 
        };
        await grain2.CreatePoll(state2);

        var result1 = await grain1.GetCurrentResults();
        var result2 = await grain2.GetCurrentResults();

        // Assert
        result1.Question.Should().Be("Poll 1?");
        result2.Question.Should().Be("Poll 2?");

        // Cleanup
        await clientHost.StopAsync();
        clientHost.Dispose();
        await siloHost.StopAsync();
        siloHost.Dispose();
    }
}
