using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using OrleansVoting.Contracts.Grains;
using OrleansVoting.Grains;

namespace OrleansVoting.Tests.Hosting;

public class SiloHostTests
{
    [Fact]
    public async Task Silo_CanStart_WithGrains()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            // Grains are auto-discovered from referenced assemblies
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
        builder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.AddMemoryGrainStorage("votes");
            // Grains are auto-discovered from referenced assemblies
        });

        var host = builder.Build();
        await host.StartAsync();

        // Act
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
        var grain = grainFactory.GetGrain<IPollGrain>("test-silo-grain");
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
        await host.StopAsync();
        host.Dispose();
    }
}
