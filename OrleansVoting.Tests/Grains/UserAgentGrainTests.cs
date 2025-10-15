using FluentAssertions;
using OrleansVoting;
using OrleansVoting.Tests.Fixtures;

namespace OrleansVoting.Tests.Grains;

public class UserAgentGrainTests : IClassFixture<TestClusterFixture>
{
    private readonly TestClusterFixture _fixture;

    public UserAgentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePoll_ReturnsValidPollId()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-1");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };

        var pollId = await grain.CreatePoll(state);

        pollId.Should().NotBeNullOrEmpty();
        pollId.Length.Should().Be(6);
    }

    [Fact]
    public async Task CreatePoll_SixthPoll_ThrowsInvalidOperationException()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-2");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };

        // Create 5 polls
        for (int i = 0; i < 5; i++)
        {
            await grain.CreatePoll(state);
        }

        // 6th should fail
        await Assert.ThrowsAsync<InvalidOperationException>(() => grain.CreatePoll(state));
    }

    [Fact]
    public async Task AddVote_FirstTime_Succeeds()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-3");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        var pollId = await grain.CreatePoll(state);

        var result = await grain.AddVote(pollId, 0);

        result.Options[0].Item2.Should().Be(1);
    }

    [Fact]
    public async Task AddVote_DoubleVote_ThrowsInvalidOperationException()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-4");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        var pollId = await grain.CreatePoll(state);

        await grain.AddVote(pollId, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => grain.AddVote(pollId, 0));
    }

    [Fact]
    public async Task Throttling_RapidRequests_ThrowsThrottlingException()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-5");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };

        // Create one poll to have a valid poll to query (counts as request #1)
        var pollId = await grain.CreatePoll(state);

        // Make 9 more rapid requests using GetPollResults (requests #2-10, at threshold)
        for (int i = 0; i < 9; i++)
        {
            await grain.GetPollResults(pollId);
        }

        // Request #11 should throw throttling exception
        await Assert.ThrowsAsync<ThrottlingException>(() => grain.GetPollResults(pollId));
    }

    [Fact]
    public async Task GetPollResults_ReturnsVotedStatus()
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-6");
        var state = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        var pollId = await grain.CreatePoll(state);

        var (results1, voted1) = await grain.GetPollResults(pollId);
        voted1.Should().BeFalse();

        await grain.AddVote(pollId, 0);

        var (results2, voted2) = await grain.GetPollResults(pollId);
        voted2.Should().BeTrue();
    }
}
