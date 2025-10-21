using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrleansVoting;
using OrleansVoting.Contracts.Grains;
using OrleansVoting.Data;
using OrleansVoting.WebApp.Pages;

namespace OrleansVoting.Tests.UI.Components;

public class PollTests : TestContext
{
    [Fact]
    public async Task OnInitialized_LoadsPollResults()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var mockUserGrain = new Mock<IUserAgentGrain>();
        var pollState = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 5), ("B", 3) }
        };
        mockUserGrain
            .Setup(x => x.GetPollResults("test123"))
            .ReturnsAsync((pollState, false));

        var mockPollGrain = new Mock<IPollGrain>();
        mockPollGrain.Setup(x => x.StartWatching(It.IsAny<IPollWatcher>())).Returns(Task.CompletedTask);

        mockGrainFactory
            .Setup(x => x.GetGrain<IUserAgentGrain>(It.IsAny<string>(), null))
            .Returns(mockUserGrain.Object);
        mockGrainFactory
            .Setup(x => x.GetGrain<IPollGrain>("test123", null))
            .Returns(mockPollGrain.Object);
        mockGrainFactory
            .Setup(x => x.CreateObjectReference<IPollWatcher>(It.IsAny<IPollWatcher>()))
            .Returns(Mock.Of<IPollWatcher>());

        var pollService = new PollService(mockGrainFactory.Object);
        pollService.Initialize("test-user");

        var mockDemoService = new Mock<DemoService>(Mock.Of<IGrainFactory>(), Mock.Of<Microsoft.Extensions.Logging.ILogger<DemoService>>());

        Services.AddSingleton(pollService);
        Services.AddSingleton(mockDemoService.Object);

        // Act
        var cut = RenderComponent<Poll>(parameters =>
            parameters.Add(p => p.PollId, "test123"));

        // Wait for initialization
        await Task.Delay(200);

        // Assert
        cut.Markup.Should().Contain("Test?");
        var buttons = cut.FindAll("button.btn-outline-secondary");
        buttons.Should().HaveCount(2);
    }

    [Fact]
    public async Task VoteForOption_CallsService()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var mockUserGrain = new Mock<IUserAgentGrain>();
        var pollState = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        var pollStateAfterVote = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 1) }
        };
        mockUserGrain
            .Setup(x => x.GetPollResults("test123"))
            .ReturnsAsync((pollState, false));
        mockUserGrain
            .Setup(x => x.AddVote("test123", 0))
            .ReturnsAsync(pollStateAfterVote);

        var mockPollGrain = new Mock<IPollGrain>();
        mockPollGrain.Setup(x => x.StartWatching(It.IsAny<IPollWatcher>())).Returns(Task.CompletedTask);

        mockGrainFactory
            .Setup(x => x.GetGrain<IUserAgentGrain>(It.IsAny<string>(), null))
            .Returns(mockUserGrain.Object);
        mockGrainFactory
            .Setup(x => x.GetGrain<IPollGrain>("test123", null))
            .Returns(mockPollGrain.Object);
        mockGrainFactory
            .Setup(x => x.CreateObjectReference<IPollWatcher>(It.IsAny<IPollWatcher>()))
            .Returns(Mock.Of<IPollWatcher>());

        var pollService = new PollService(mockGrainFactory.Object);
        pollService.Initialize("test-user");

        var mockDemoService = new Mock<DemoService>(Mock.Of<IGrainFactory>(), Mock.Of<Microsoft.Extensions.Logging.ILogger<DemoService>>());

        Services.AddSingleton(pollService);
        Services.AddSingleton(mockDemoService.Object);

        var cut = RenderComponent<Poll>(parameters =>
            parameters.Add(p => p.PollId, "test123"));
        await Task.Delay(200);

        // Act
        cut.Find("button.btn-outline-secondary").Click();
        await Task.Delay(100);

        // Assert
        mockUserGrain.Verify(x => x.AddVote("test123", 0), Times.Once);
        cut.Markup.Should().Contain("progress-bar"); // Shows results after voting
    }

    [Fact]
    public async Task OnInitialized_WithError_DisplaysErrorMessage()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var mockUserGrain = new Mock<IUserAgentGrain>();
        mockUserGrain
            .Setup(x => x.GetPollResults("test123"))
            .ThrowsAsync(new Exception("Poll not found"));

        mockGrainFactory
            .Setup(x => x.GetGrain<IUserAgentGrain>(It.IsAny<string>(), null))
            .Returns(mockUserGrain.Object);

        var pollService = new PollService(mockGrainFactory.Object);
        pollService.Initialize("test-user");

        var mockDemoService = new Mock<DemoService>(Mock.Of<IGrainFactory>(), Mock.Of<Microsoft.Extensions.Logging.ILogger<DemoService>>());

        Services.AddSingleton(pollService);
        Services.AddSingleton(mockDemoService.Object);

        // Act
        var cut = RenderComponent<Poll>(parameters =>
            parameters.Add(p => p.PollId, "test123"));
        await Task.Delay(200);

        // Assert
        cut.Markup.Should().Contain("alert-danger");
        cut.Markup.Should().Contain("Poll not found");
    }
}
