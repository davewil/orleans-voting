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

public class SiloHostTests
{
    [Fact]
    public async Task Silo_CanStart_WithGrains()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
        });

        var host = builder.Build();

        // Act
        await host.StartAsync();

        // Assert
        host.Services.GetRequiredService<IGrainFactory>().Should().NotBeNull();

        // Cleanup
        await host.StopAsync();
        host.Dispose();
    }

    [Fact]
    public async Task Silo_GrainsActivate_Successfully()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.AddMemoryGrainStorage("votes");
        });

        var host = builder.Build();
        await host.StartAsync();

        // Act
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
        var grain = grainFactory.GetGrain<IPollGrain>("test-silo-grain");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("Option A", 0) }
        };
        await grain.CreatePoll(state);
        var result = await grain.GetCurrentResults();

        // Assert
        result.Question.Should().Be("Test?");

        // Cleanup
        await host.StopAsync();
        host.Dispose();
    }
}
