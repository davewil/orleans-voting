using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrleansVoting;
using OrleansVoting.Data;
using OrleansVoting.Service.Pages;

namespace OrleansVoting.Tests.Components;

public class PollEditorTests : TestContext
{
    [Fact]
    public void Render_DisplaysForm()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var pollService = new PollService(mockGrainFactory.Object);
        Services.AddSingleton(pollService);
        Services.AddSingleton(Mock.Of<NavigationManager>());

        // Act
        var cut = RenderComponent<PollEditor>();

        // Assert
        cut.Find("#pollTitle").Should().NotBeNull();
        cut.Find("input[placeholder='Option value']").Should().NotBeNull();
    }

    [Fact]
    public void AddOption_WithValidInput_AddsToList()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var pollService = new PollService(mockGrainFactory.Object);
        Services.AddSingleton(pollService);
        Services.AddSingleton(Mock.Of<NavigationManager>());

        var cut = RenderComponent<PollEditor>();

        // Act
        cut.Find("input[placeholder='Option value']").Change("Option 1");
        cut.Find("button#button-add").Click();

        // Assert
        var optionInputs = cut.FindAll("input[type='text']");
        optionInputs.Should().Contain(i => i.GetAttribute("value") == "Option 1");
    }

    [Fact]
    public async Task CreatePoll_WithValidData_CallsService()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var mockUserGrain = new Mock<IUserAgentGrain>();
        mockUserGrain
            .Setup(x => x.CreatePoll(It.IsAny<PollState>()))
            .ReturnsAsync("abc123");
        mockGrainFactory
            .Setup(x => x.GetGrain<IUserAgentGrain>(It.IsAny<string>(), null))
            .Returns(mockUserGrain.Object);

        var pollService = new PollService(mockGrainFactory.Object);
        pollService.Initialize("test-user");

        Services.AddSingleton(pollService);
        var navMan = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<PollEditor>();

        // Act
        cut.Find("#pollTitle").Change("My Poll");
        cut.Find("input[placeholder='Option value']").Change("Option A");
        cut.Find("button#button-add").Click();
        cut.Find("button.btn-primary").Click();

        // Wait for async operation
        await Task.Delay(100);

        // Assert
        mockUserGrain.Verify(x => x.CreatePoll(
            It.Is<PollState>(p => p.Question == "My Poll" && p.Options.Any(o => o.Option == "Option A"))), Times.Once);
    }

    [Fact]
    public void DemoAutofill_PopulatesFields()
    {
        // Arrange
        var mockGrainFactory = new Mock<IGrainFactory>();
        var pollService = new PollService(mockGrainFactory.Object);
        Services.AddSingleton(pollService);
        Services.AddSingleton(Mock.Of<NavigationManager>());

        var cut = RenderComponent<PollEditor>();

        // Act
        cut.Find("button.btn-danger").Click();

        // Assert
        cut.Find("#pollTitle").GetAttribute("value").Should().Contain("favorite color");
        var optionInputs = cut.FindAll("input[type='text']");
        optionInputs.Should().HaveCountGreaterThan(3);
    }
}
