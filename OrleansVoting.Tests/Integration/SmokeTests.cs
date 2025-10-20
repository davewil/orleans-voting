using FluentAssertions;
using OrleansVoting;
using OrleansVoting.Data;
using OrleansVoting.Tests.Fixtures;

namespace OrleansVoting.Tests.Integration;

public class SmokeTests : IClassFixture<TestClusterFixture>
{
    private readonly TestClusterFixture _fixture;

    public SmokeTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EndToEnd_CreatePollAndVote_Success()
    {
        // Arrange
        var pollService = new PollService(_fixture.Cluster.GrainFactory);
        pollService.Initialize($"test-user-{Guid.NewGuid()}");

        // Act - Create poll
        var pollId = await pollService.CreatePollAsync(
            "What's your favorite color?",
            new List<string> { "Red", "Blue", "Green" }
        );

        // Act - Get poll
        var (results, voted) = await pollService.GetPollResultsAsync(pollId);

        // Assert - Poll created correctly
        results.Question.Should().Be("What's your favorite color?");
        results.Options.Should().HaveCount(3);
        voted.Should().BeFalse();

        // Act - Vote
        var voteResult = await pollService.AddVoteAsync(pollId, 0);

        // Assert - Vote recorded
        voteResult.Options[0].Item2.Should().Be(1);

        // Act - Check voted status
        var (_, votedNow) = await pollService.GetPollResultsAsync(pollId);
        votedNow.Should().BeTrue();
    }

    [Fact]
    public async Task RealTimeUpdates_ObserverReceivesNotification()
    {
        // Arrange
        var user1 = new PollService(_fixture.Cluster.GrainFactory);
        user1.Initialize($"test-user-{Guid.NewGuid()}");

        var pollId = await user1.CreatePollAsync(
            "Test?",
            new List<string> { "A", "B" }
        );

        var watcher = new TestPollWatcher();
        var subscription = await user1.WatchPoll(pollId, watcher);

        // Act - Different user votes
        var user2 = new PollService(_fixture.Cluster.GrainFactory);
        user2.Initialize($"test-user-{Guid.NewGuid()}");
        await user2.AddVoteAsync(pollId, 0);

        // Wait for notification to propagate
        await Task.Delay(1000);

        // Assert
        watcher.UpdateCount.Should().BeGreaterThan(0);
        watcher.LastState.Should().NotBeNull();
        watcher.LastState!.Options[0].Item2.Should().BeGreaterThan(0);

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Throttling_EnforcedAcrossRequests()
    {
        // Arrange
        var service = new PollService(_fixture.Cluster.GrainFactory);
        service.Initialize($"throttle-user-{Guid.NewGuid()}");

        // Act - Make 5 requests to create polls (limit is 5 polls per user)
        for (int i = 0; i < 5; i++)
        {
            await service.CreatePollAsync($"Poll {i}", new List<string> { "A" });
        }

        // Act & Assert - 6th poll creation should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePollAsync("Poll 6", new List<string> { "A" })
        );
    }
}
