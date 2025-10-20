using FluentAssertions;
using Moq;
using OrleansVoting;
using OrleansVoting.Contracts.Grains;
using OrleansVoting.Data;

namespace OrleansVoting.Tests.Services;

public class PollServiceTests
{
    private readonly Mock<IGrainFactory> _mockGrainFactory;
    private readonly Mock<IUserAgentGrain> _mockUserGrain;
    private readonly PollService _sut;

    public PollServiceTests()
    {
        _mockGrainFactory = new Mock<IGrainFactory>();
        _mockUserGrain = new Mock<IUserAgentGrain>();

        _mockGrainFactory
            .Setup(x => x.GetGrain<IUserAgentGrain>(It.IsAny<string>(), null))
            .Returns(_mockUserGrain.Object);

        _sut = new PollService(_mockGrainFactory.Object);
    }

    [Fact]
    public void Initialize_SetsUserAgentGrain()
    {
        // Act
        _sut.Initialize("192.168.1.1");

        // Assert - subsequent calls should work
        _mockGrainFactory.Verify(x => x.GetGrain<IUserAgentGrain>("192.168.1.1", null), Times.Once);
    }

    [Fact]
    public async Task CreatePollAsync_CallsUserAgentGrain()
    {
        // Arrange
        _sut.Initialize("192.168.1.1");
        _mockUserGrain
            .Setup(x => x.CreatePoll(It.IsAny<PollState>()))
            .ReturnsAsync("abc123");

        // Act
        var result = await _sut.CreatePollAsync("Question?", new List<string> { "A", "B" });

        // Assert
        result.Should().Be("abc123");
        _mockUserGrain.Verify(x => x.CreatePoll(It.Is<PollState>(
            p => p.Question == "Question?" && p.Options.Count == 2
        )), Times.Once);
    }

    [Fact]
    public async Task GetPollResultsAsync_ReturnsStateAndVoted()
    {
        // Arrange
        _sut.Initialize("192.168.1.1");
        var expectedState = new PollState
        {
            Question = "Q",
            Options = new List<(string, int)>()
        };
        _mockUserGrain
            .Setup(x => x.GetPollResults("poll1"))
            .ReturnsAsync((expectedState, true));

        // Act
        var (results, voted) = await _sut.GetPollResultsAsync("poll1");

        // Assert
        results.Should().Be(expectedState);
        voted.Should().BeTrue();
    }

    [Fact]
    public async Task AddVoteAsync_CallsUserAgentGrain()
    {
        // Arrange
        _sut.Initialize("192.168.1.1");
        var expectedState = new PollState
        {
            Question = "Q",
            Options = new List<(string, int)>()
        };
        _mockUserGrain
            .Setup(x => x.AddVote("poll1", 0))
            .ReturnsAsync(expectedState);

        // Act
        var result = await _sut.AddVoteAsync("poll1", 0);

        // Assert
        result.Should().Be(expectedState);
    }

    [Fact]
    public async Task WatchPoll_CreatesObserverReference()
    {
        // Arrange
        var mockPollGrain = new Mock<IPollGrain>();
        _mockGrainFactory
            .Setup(x => x.GetGrain<IPollGrain>("poll1", null))
            .Returns(mockPollGrain.Object);

        var mockWatcherRef = new Mock<IPollWatcher>();
        _mockGrainFactory
            .Setup(x => x.CreateObjectReference<IPollWatcher>(It.IsAny<IPollWatcher>()))
            .Returns(mockWatcherRef.Object);

        var watcher = new Mock<IPollWatcher>().Object;

        // Act
        var subscription = await _sut.WatchPoll("poll1", watcher);

        // Assert
        subscription.Should().NotBeNull();
        _mockGrainFactory.Verify(x => x.CreateObjectReference<IPollWatcher>(watcher), Times.Once);

        // Cleanup
        await subscription.DisposeAsync();
    }
}
