using FluentAssertions;
using OrleansVoting;
using OrleansVoting.Tests.Fixtures;

namespace OrleansVoting.Tests.Grains;

public class PollGrainTests : IClassFixture<TestClusterFixture>
{
    private readonly TestClusterFixture _fixture;

    public PollGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePoll_ValidState_PersistsCorrectly()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-1");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0), ("B", 0) }
        };

        // Act
        await grain.CreatePoll(state);
        var result = await grain.GetCurrentResults();

        // Assert
        result.Question.Should().Be("Test?");
        result.Options.Should().HaveCount(2);
        result.Options[0].Item1.Should().Be("A");
        result.Options[0].Item2.Should().Be(0);
        result.Options[1].Item1.Should().Be("B");
        result.Options[1].Item2.Should().Be(0);
    }

    [Fact]
    public async Task AddVote_ValidOption_IncrementsCount()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-2");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0), ("B", 0) }
        };
        await grain.CreatePoll(state);

        // Act
        await grain.AddVote(0);
        var result = await grain.GetCurrentResults();

        // Assert
        result.Options[0].Item2.Should().Be(1);
        result.Options[1].Item2.Should().Be(0);
    }

    [Fact]
    public async Task AddVote_InvalidOption_ThrowsKeyNotFoundException()
    {
        // This test ensures contract behavior is maintained
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-3");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        await grain.CreatePoll(state);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => grain.AddVote(5));
    }

    [Fact]
    public async Task AddVote_NotifiesObservers()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-4");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        await grain.CreatePoll(state);

        var observer = new TestPollWatcher();
        var observerRef = _fixture.Cluster.GrainFactory.CreateObjectReference<IPollWatcher>(observer);
        await grain.StartWatching(observerRef);

        // Act
        await grain.AddVote(0);
        await Task.Delay(100); // Allow notification to propagate

        // Assert
        observer.UpdateCount.Should().Be(1);
        observer.LastState.Should().NotBeNull();
        observer.LastState!.Options[0].Item2.Should().Be(1);
    }
}
