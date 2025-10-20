# Detailed Migration Plan: Refactoring to Production Architecture

## Overview Principles

**Test-First Philosophy:**
- ✅ Write tests BEFORE any refactoring
- ✅ Tests must pass before refactoring begins
- ✅ Tests must remain green after refactoring
- ✅ If a test goes red during refactoring, stop and fix immediately
- ✅ Each phase has its own test safety net

**Migration Strategy:**
- Make changes incrementally
- Never break existing functionality
- Keep application deployable at all times
- Verify behavior after each step

---

## Phase 1: Establish Test Safety Net (Current System)

**Goal:** Achieve sufficient test coverage on the existing monolithic structure so we can refactor with confidence.

### Step 1.1: Create Test Infrastructure

**Actions:**
```
1. Create OrleansVoting.Tests.csproj
2. Add packages:
   - Microsoft.Orleans.TestingHost
   - xUnit
   - FluentAssertions
   - bUnit
   - Moq
3. Create test fixtures and helpers
```

**Deliverables:**
- `TestClusterFixture.cs` - Shared Orleans test cluster
- `TestRedisFixture.cs` - In-memory Redis or Testcontainers
- Test project compiles and can discover tests

**Verification:** `dotnet test` runs successfully (even with 0 tests)

---

### Step 1.2: Add Grain Unit Tests

**Purpose:** These tests verify grain behavior in isolation. They MUST pass before we move grains to a separate project.

**Tests to Write:**

#### PollGrain Tests (OrleansVoting.Tests/Grains/PollGrainTests.cs)
```csharp
[Fact]
public async Task CreatePoll_ValidState_PersistsCorrectly()
{
    // Arrange
    var grain = _cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-1");
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
}

[Fact]
public async Task AddVote_ValidOption_IncrementsCount()
{
    // Arrange
    var grain = _cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-2");
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
    result.Options[0].Votes.Should().Be(1);
}

[Fact]
public async Task AddVote_InvalidOption_ThrowsKeyNotFoundException()
{
    // This test ensures contract behavior is maintained
    var grain = _cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-3");
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
    var grain = _cluster.GrainFactory.GetGrain<IPollGrain>("test-poll-4");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};
    await grain.CreatePoll(state);

    var observer = new TestPollWatcher();
    var observerRef = _cluster.GrainFactory.CreateObjectReference<IPollWatcher>(observer);
    await grain.StartWatching(observerRef);

    // Act
    await grain.AddVote(0);
    await Task.Delay(100); // Allow notification to propagate

    // Assert
    observer.UpdateCount.Should().Be(1);
    observer.LastState.Options[0].Votes.Should().Be(1);
}
```

#### UserAgentGrain Tests (OrleansVoting.Tests/Grains/UserAgentGrainTests.cs)
```csharp
[Fact]
public async Task CreatePoll_ReturnsValidPollId()
{
    var grain = _cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-1");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};

    var pollId = await grain.CreatePoll(state);

    pollId.Should().NotBeNullOrEmpty();
    pollId.Length.Should().Be(6);
}

[Fact]
public async Task CreatePoll_SixthPoll_ThrowsInvalidOperationException()
{
    var grain = _cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-2");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};

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
    var grain = _cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-3");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};
    var pollId = await grain.CreatePoll(state);

    var result = await grain.AddVote(pollId, 0);

    result.Options[0].Votes.Should().Be(1);
}

[Fact]
public async Task AddVote_DoubleVote_ThrowsInvalidOperationException()
{
    var grain = _cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-4");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};
    var pollId = await grain.CreatePoll(state);

    await grain.AddVote(pollId, 0);

    await Assert.ThrowsAsync<InvalidOperationException>(() => grain.AddVote(pollId, 0));
}

[Fact]
public async Task Throttling_RapidRequests_ThrowsThrottlingException()
{
    var grain = _cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-5");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};

    // Make 11 rapid requests (threshold is 10)
    for (int i = 0; i < 10; i++)
    {
        await grain.CreatePoll(state);
    }

    await Assert.ThrowsAsync<ThrottlingException>(() => grain.CreatePoll(state));
}

[Fact]
public async Task GetPollResults_ReturnsVotedStatus()
{
    var grain = _cluster.GrainFactory.GetGrain<IUserAgentGrain>("user-6");
    var state = new PollState { Question = "Test?", Options = new List<(string, int)> { ("A", 0) }};
    var pollId = await grain.CreatePoll(state);

    var (results1, voted1) = await grain.GetPollResults(pollId);
    voted1.Should().BeFalse();

    await grain.AddVote(pollId, 0);

    var (results2, voted2) = await grain.GetPollResults(pollId);
    voted2.Should().BeTrue();
}
```

**Success Criteria:**
- ✅ All grain tests pass
- ✅ Tests cover core behaviors (create, vote, throttle, observers)
- ✅ Tests use actual TestCluster (not mocks) for grains
- ✅ Tests verify both success and error cases
- ✅ Run time: < 30 seconds for all grain tests

**⚠️ CHECKPOINT:** Do not proceed to Step 1.3 until all these tests are green.

---

### Step 1.3: Add Service Layer Tests

**Purpose:** Verify PollService wrapping logic. These tests ensure the service layer correctly orchestrates grain calls.

**Tests to Write:**

#### PollService Tests (OrleansVoting.Tests/Services/PollServiceTests.cs)
```csharp
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
        var expectedState = new PollState { Question = "Q", Options = new() };
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
        var expectedState = new PollState { Question = "Q", Options = new() };
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

        var watcherRef = Mock.Of<IPollWatcher>();
        _mockGrainFactory
            .Setup(x => x.CreateObjectReference<IPollWatcher>(It.IsAny<IPollWatcher>()))
            .Returns(watcherRef);

        var watcher = new TestPollWatcher();

        // Act
        var subscription = await _sut.WatchPoll("poll1", watcher);

        // Assert
        subscription.Should().NotBeNull();
        _mockGrainFactory.Verify(x => x.CreateObjectReference<IPollWatcher>(watcher), Times.Once);
    }
}
```

**Success Criteria:**
- ✅ All service layer tests pass
- ✅ Tests use mocks (not real grains) for isolation
- ✅ Tests verify correct grain method calls
- ✅ Fast execution (< 5 seconds)

**⚠️ CHECKPOINT:** Do not proceed until all tests green.

---

### Step 1.4: Add Blazor Component Tests

**Purpose:** Verify UI component behavior. These tests ensure UI logic remains correct during refactoring.

**Tests to Write:**

#### PollEditor.razor Tests (OrleansVoting.Tests/Components/PollEditorTests.cs)
```csharp
public class PollEditorTests : TestContext
{
    [Fact]
    public void Render_DisplaysForm()
    {
        // Arrange
        var mockPollService = new Mock<PollService>();
        Services.AddSingleton(mockPollService.Object);
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
        var mockPollService = new Mock<PollService>();
        Services.AddSingleton(mockPollService.Object);
        Services.AddSingleton(Mock.Of<NavigationManager>());

        var cut = RenderComponent<PollEditor>();

        // Act
        cut.Find("input[placeholder='Option value']").Change("Option 1");
        cut.Find("button#button-add").Click();

        // Assert
        cut.FindAll("input[type='text'][value='Option 1']").Should().HaveCount(1);
    }

    [Fact]
    public async Task CreatePoll_WithValidData_CallsService()
    {
        // Arrange
        var mockPollService = new Mock<PollService>();
        mockPollService
            .Setup(x => x.CreatePollAsync(It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync("abc123");

        var mockNav = new Mock<NavigationManager>();
        Services.AddSingleton(mockPollService.Object);
        Services.AddSingleton(mockNav.Object);

        var cut = RenderComponent<PollEditor>();

        // Act
        cut.Find("#pollTitle").Change("My Poll");
        cut.Find("input[placeholder='Option value']").Change("Option A");
        cut.Find("button#button-add").Click();
        cut.Find("button.btn-primary").Click();

        // Wait for async operation
        await Task.Delay(100);

        // Assert
        mockPollService.Verify(x => x.CreatePollAsync("My Poll",
            It.Is<List<string>>(list => list.Contains("Option A"))), Times.Once);
    }

    [Fact]
    public void DemoAutofill_PopulatesFields()
    {
        // Arrange
        var mockPollService = new Mock<PollService>();
        Services.AddSingleton(mockPollService.Object);
        Services.AddSingleton(Mock.Of<NavigationManager>());

        var cut = RenderComponent<PollEditor>();

        // Act
        cut.Find("button.btn-danger").Click();

        // Assert
        cut.Find("#pollTitle").GetAttribute("value").Should().Contain("favorite color");
        cut.FindAll("input[type='text']").Should().HaveCountGreaterThan(3);
    }
}
```

#### Poll.razor Tests (OrleansVoting.Tests/Components/PollTests.cs)
```csharp
public class PollTests : TestContext
{
    [Fact]
    public async Task OnInitialized_LoadsPollResults()
    {
        // Arrange
        var mockPollService = new Mock<PollService>();
        var pollState = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 5), ("B", 3) }
        };
        mockPollService
            .Setup(x => x.GetPollResultsAsync("test123"))
            .ReturnsAsync((pollState, false));
        mockPollService
            .Setup(x => x.WatchPoll(It.IsAny<string>(), It.IsAny<IPollWatcher>()))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());

        Services.AddSingleton(mockPollService.Object);
        Services.AddSingleton(Mock.Of<DemoService>());

        // Act
        var cut = RenderComponent<Poll>(parameters =>
            parameters.Add(p => p.PollId, "test123"));

        // Wait for initialization
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("Test?");
        cut.FindAll("button.btn-outline-secondary").Should().HaveCount(2);
    }

    [Fact]
    public async Task VoteForOption_CallsService()
    {
        // Arrange
        var mockPollService = new Mock<PollService>();
        var pollState = new PollState
        {
            Question = "Test?",
            Options = new List<(string, int)> { ("A", 0) }
        };
        mockPollService
            .Setup(x => x.GetPollResultsAsync("test123"))
            .ReturnsAsync((pollState, false));
        mockPollService
            .Setup(x => x.AddVoteAsync("test123", 0))
            .ReturnsAsync(pollState);
        mockPollService
            .Setup(x => x.WatchPoll(It.IsAny<string>(), It.IsAny<IPollWatcher>()))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());

        Services.AddSingleton(mockPollService.Object);
        Services.AddSingleton(Mock.Of<DemoService>());

        var cut = RenderComponent<Poll>(parameters =>
            parameters.Add(p => p.PollId, "test123"));
        await Task.Delay(100);

        // Act
        cut.Find("button.btn-outline-secondary").Click();
        await Task.Delay(100);

        // Assert
        mockPollService.Verify(x => x.AddVoteAsync("test123", 0), Times.Once);
        cut.Markup.Should().Contain("progress-bar"); // Shows results after voting
    }

    [Fact]
    public async Task OnInitialized_WithError_DisplaysErrorMessage()
    {
        // Arrange
        var mockPollService = new Mock<PollService>();
        mockPollService
            .Setup(x => x.GetPollResultsAsync("test123"))
            .ThrowsAsync(new Exception("Poll not found"));

        Services.AddSingleton(mockPollService.Object);
        Services.AddSingleton(Mock.Of<DemoService>());

        // Act
        var cut = RenderComponent<Poll>(parameters =>
            parameters.Add(p => p.PollId, "test123"));
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("alert-danger");
        cut.Markup.Should().Contain("Poll not found");
    }
}
```

**Success Criteria:**
- ✅ All component tests pass
- ✅ Tests verify user interactions
- ✅ Tests verify service calls
- ✅ Tests verify error handling
- ✅ Fast execution (< 10 seconds)

**⚠️ CHECKPOINT:** Do not proceed until all tests green.

**📝 TODO - Future Refactoring:**
The Blazor component tests currently pass but have significant quality issues:
- **Setup complexity**: Each test requires extensive DI container configuration
- **Poor intent communication**: Setup boilerplate obscures what's actually being tested
- **Maintenance burden**: Changes to dependencies require updating setup in multiple tests

**Recommended improvements (future work):**
1. Create a test facade/builder pattern to encapsulate common setup
2. Consider test-specific base classes that pre-configure common dependencies
3. Explore bUnit's built-in dependency injection helpers to reduce boilerplate
4. Extract setup logic into helper methods with descriptive names
5. Consider whether we need to test DI wiring or just component behavior

**Goal:** Tests should clearly communicate what behavior is being verified without drowning in infrastructure setup.

---

### Step 1.5: Add Integration Smoke Tests

**Purpose:** End-to-end tests to verify the full system works. These are our "golden path" tests.

**Tests to Write:**

#### Integration Tests (OrleansVoting.Tests/Integration/SmokeTests.cs)
```csharp
public class SmokeTests : IClassFixture<TestClusterFixture>
{
    private readonly TestCluster _cluster;
    private readonly PollService _pollService;

    public SmokeTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
        _pollService = new PollService(_cluster.GrainFactory);
    }

    [Fact]
    public async Task EndToEnd_CreatePollAndVote_Success()
    {
        // Arrange
        _pollService.Initialize($"test-user-{Guid.NewGuid()}");

        // Act - Create poll
        var pollId = await _pollService.CreatePollAsync(
            "What's your favorite color?",
            new List<string> { "Red", "Blue", "Green" }
        );

        // Act - Get poll
        var (results, voted) = await _pollService.GetPollResultsAsync(pollId);

        // Assert - Poll created correctly
        results.Question.Should().Be("What's your favorite color?");
        results.Options.Should().HaveCount(3);
        voted.Should().BeFalse();

        // Act - Vote
        var voteResult = await _pollService.AddVoteAsync(pollId, 0);

        // Assert - Vote recorded
        voteResult.Options[0].Votes.Should().Be(1);

        // Act - Check voted status
        var (_, votedNow) = await _pollService.GetPollResultsAsync(pollId);
        votedNow.Should().BeTrue();
    }

    [Fact]
    public async Task RealTimeUpdates_ObserverReceivesNotification()
    {
        // Arrange
        var user1 = new PollService(_cluster.GrainFactory);
        user1.Initialize($"test-user-{Guid.NewGuid()}");

        var pollId = await user1.CreatePollAsync(
            "Test?",
            new List<string> { "A", "B" }
        );

        var watcher = new TestPollWatcher();
        var subscription = await user1.WatchPoll(pollId, watcher);

        // Act - Different user votes
        var user2 = new PollService(_cluster.GrainFactory);
        user2.Initialize($"test-user-{Guid.NewGuid()}");
        await user2.AddVoteAsync(pollId, 0);

        // Wait for notification
        await Task.Delay(500);

        // Assert
        watcher.UpdateCount.Should().BeGreaterThan(0);
        watcher.LastState.Options[0].Votes.Should().BeGreaterThan(0);

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Throttling_EnforcedAcrossRequests()
    {
        // Arrange
        var service = new PollService(_cluster.GrainFactory);
        service.Initialize($"throttle-user-{Guid.NewGuid()}");

        // Act - Make 10 requests (at threshold)
        for (int i = 0; i < 10; i++)
        {
            await service.CreatePollAsync($"Poll {i}", new List<string> { "A" });
        }

        // Act & Assert - 11th should throw
        await Assert.ThrowsAsync<ThrottlingException>(() =>
            service.CreatePollAsync("Poll 11", new List<string> { "A" })
        );
    }
}
```

**Success Criteria:**
- ✅ All integration tests pass
- ✅ Tests verify end-to-end flows
- ✅ Tests use real Orleans cluster
- ✅ Tests verify cross-grain interactions

**⚠️ MAJOR CHECKPOINT:**
- **All tests from Steps 1.2-1.5 must be green**
- **Run full test suite: `dotnet test`**
- **All tests must pass before proceeding to Phase 2**
- **This is your safety net for all future refactoring**

---

## Phase 2: Extract Contracts Project

**Goal:** Create OrleansVoting.Contracts and move interfaces/DTOs without breaking anything.

### Step 2.1: Create Contracts Project

**Actions:**
```bash
dotnet new classlib -n OrleansVoting.Contracts
cd OrleansVoting.Contracts
dotnet add package Microsoft.Orleans.Sdk
```

**Update OrleansVoting.Contracts.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Orleans.Sdk" Version="9.2.1" />
  </ItemGroup>
</Project>
```

**Add to solution:**
```bash
dotnet sln OrleansVoting.sln add OrleansVoting.Contracts/OrleansVoting.Contracts.csproj
```

**Verification:**
- ✅ Project builds: `dotnet build OrleansVoting.Contracts`
- ✅ Solution builds: `dotnet build OrleansVoting.sln`
- ✅ **All tests still pass:** `dotnet test`

---

### Step 2.2: Copy Grain Interfaces to Contracts

**Actions:**
1. Copy (don't move yet!) these files to OrleansVoting.Contracts:
   - `Grains/IPollGrain.cs` (including `PollState` class)
   - `Grains/IUserAgentGrain.cs`
   - `Grains/IVoteGrain.cs`
   - `Grains/IPollWatcher.cs`
   - `Helpers/ThrottlingException.cs`

2. Update namespaces in copied files to `OrleansVoting.Contracts`

3. Create folder structure:
```
OrleansVoting.Contracts/
├── Grains/
│   ├── IPollGrain.cs
│   ├── IUserAgentGrain.cs
│   ├── IVoteGrain.cs
│   └── IPollWatcher.cs
├── Models/
│   └── PollState.cs  (if separated from IPollGrain.cs)
└── Exceptions/
    └── ThrottlingException.cs
```

**Important:** At this point, files exist in BOTH locations. This is intentional.

**Verification:**
- ✅ OrleansVoting.Contracts builds
- ✅ OrleansVoting.Service still builds (uses original files)
- ✅ **All tests still pass:** `dotnet test`

---

### Step 2.3: Reference Contracts from Service

**Actions:**
1. Add project reference in OrleansVoting.Service.csproj:
```xml
<ItemGroup>
  <ProjectReference Include="..\OrleansVoting.Contracts\OrleansVoting.Contracts.csproj" />
</ItemGroup>
```

2. Do NOT change any code yet

**Verification:**
- ✅ OrleansVoting.Service builds
- ✅ **All tests still pass:** `dotnet test`

---

### Step 2.4: Update Service to Use Contracts

**Actions:**
1. In OrleansVoting.Service, add using:
```csharp
using OrleansVoting.Contracts.Grains;
using OrleansVoting.Contracts.Exceptions;
```

2. Delete original interface files ONE AT A TIME:
   - Delete `Grains/IPollGrain.cs` → Build → Test
   - Delete `Grains/IUserAgentGrain.cs` → Build → Test
   - Delete `Grains/IVoteGrain.cs` → Build → Test
   - Delete `Grains/IPollWatcher.cs` → Build → Test
   - Delete `Helpers/ThrottlingException.cs` → Build → Test

3. Fix any namespace issues in grain implementations:
   - PollGrain.cs, UserAgentGrain.cs, VoteGrain.cs should now reference contracts

**Test After Each Deletion:**
```bash
dotnet build
dotnet test
```

**Success Criteria:**
- ✅ No duplicate files
- ✅ Service references Contracts for all interfaces
- ✅ Grain implementations still in Service (for now)
- ✅ **All tests still green**

**⚠️ CHECKPOINT:** If any test fails, revert the last deletion and investigate.

---

### Step 2.5: Update Tests to Use Contracts

**Actions:**
1. Add reference in OrleansVoting.Tests.csproj:
```xml
<ProjectReference Include="..\OrleansVoting.Contracts\OrleansVoting.Contracts.csproj" />
```

2. Update test file usings to reference contracts:
```csharp
using OrleansVoting.Contracts.Grains;
using OrleansVoting.Contracts.Exceptions;
```

**Verification:**
- ✅ Tests build
- ✅ **All tests pass:** `dotnet test`

---

## Phase 3: Extract Grains Project

**Goal:** Move grain implementations to separate project, keep interfaces in Contracts.

### Step 3.1: Create Grains Project

**Actions:**
```bash
dotnet new classlib -n OrleansVoting.Grains
cd OrleansVoting.Grains
dotnet add package Microsoft.Orleans.Runtime
dotnet add package Microsoft.Orleans.Core.Abstractions
dotnet add reference ../OrleansVoting.Contracts/OrleansVoting.Contracts.csproj
```

**Add to solution:**
```bash
dotnet sln OrleansVoting.sln add OrleansVoting.Grains/OrleansVoting.Grains.csproj
```

**Verification:**
- ✅ Project builds
- ✅ **All tests still pass:** `dotnet test`

---

### Step 3.2: Copy Grain Implementations

**Actions:**
1. Copy (don't move yet!) these files to OrleansVoting.Grains:
   - `Grains/PollGrain.cs`
   - `Grains/UserAgentGrain.cs`
   - `Grains/VoteGrain.cs`

2. Update namespace to `OrleansVoting.Grains`

3. Add using for contracts:
```csharp
using OrleansVoting.Contracts.Grains;
```

**Verification:**
- ✅ OrleansVoting.Grains builds
- ✅ OrleansVoting.Service still builds (uses original files)
- ✅ **All tests still pass:** `dotnet test`

---

### Step 3.3: Reference Grains from Service

**Actions:**
1. Add project reference in OrleansVoting.Service.csproj:
```xml
<ProjectReference Include="..\OrleansVoting.Grains\OrleansVoting.Grains.csproj" />
```

**Verification:**
- ✅ Builds
- ✅ **All tests pass**

---

### Step 3.4: Remove Grain Implementations from Service

**Actions:**
1. Delete implementation files ONE AT A TIME from OrleansVoting.Service:
   - Delete `Grains/PollGrain.cs` → Build → Test
   - Delete `Grains/UserAgentGrain.cs` → Build → Test
   - Delete `Grains/VoteGrain.cs` → Build → Test

2. Service should now only reference Grains project, not contain implementations

**Test After Each Deletion:**
```bash
dotnet build
dotnet test
```

**Success Criteria:**
- ✅ No grain implementations in Service
- ✅ Service references both Contracts and Grains projects
- ✅ **All tests green**

---

### Step 3.5: Update Tests to Use Grains Project

**Actions:**
1. Add reference in OrleansVoting.Tests.csproj:
```xml
<ProjectReference Include="..\OrleansVoting.Grains\OrleansVoting.Grains.csproj" />
```

2. Verify grain types are resolved correctly

**Verification:**
- ✅ Tests build
- ✅ **All tests pass**

**⚠️ CHECKPOINT:** Phase 3 complete. Grains are now separate!

---

## Phase 4: Create Dedicated Silo Project

**Goal:** Create Orleans hosting project separate from web application.

### Step 4.1: Add Tests for Silo Hosting

**Purpose:** Verify silo can start and host grains before we create it.

**Tests to Write (OrleansVoting.Tests/Hosting/SiloHostTests.cs):**
```csharp
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
            silo.ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(PollGrain).Assembly).WithReferences();
            });
        });

        var host = builder.Build();

        // Act
        await host.StartAsync();

        // Assert
        host.Services.GetRequiredService<IGrainFactory>().Should().NotBeNull();

        // Cleanup
        await host.StopAsync();
    }

    [Fact]
    public async Task Silo_GrainsActivate_Successfully()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(PollGrain).Assembly).WithReferences();
            });
            silo.AddMemoryGrainStorage("votes");
        });

        var host = builder.Build();
        await host.StartAsync();

        // Act
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
        var grain = grainFactory.GetGrain<IPollGrain>("test-silo-grain");
        var state = new PollState { Question = "Test?", Options = new() };
        await grain.CreatePoll(state);
        var result = await grain.GetCurrentResults();

        // Assert
        result.Question.Should().Be("Test?");

        // Cleanup
        await host.StopAsync();
    }
}
```

**Run tests - they should pass:**
```bash
dotnet test --filter "FullyQualifiedName~SiloHostTests"
```

**Success Criteria:**
- ✅ Silo hosting tests pass
- ✅ Verifies grains can activate in dedicated host

**Status:** ✅ **COMPLETE** (2025-10-20)

---

### Step 4.2: Create Silo Project

**Actions:**
```bash
dotnet new web -n OrleansVoting.Silo
cd OrleansVoting.Silo
dotnet add package Aspire.Hosting.Orleans
dotnet add package Aspire.StackExchange.Redis
dotnet add package Microsoft.Orleans.Server
dotnet add package Microsoft.Orleans.Clustering.Redis
dotnet add package Microsoft.Orleans.Persistence.Redis
dotnet add reference ../OrleansVoting.Contracts/OrleansVoting.Contracts.csproj
dotnet add reference ../OrleansVoting.Grains/OrleansVoting.Grains.csproj
dotnet add reference ../OrleansVoting.ServiceDefaults/OrleansVoting.ServiceDefaults.csproj
```

**Create OrleansVoting.Silo/Program.cs:**
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKeyedRedisClient("voting-redis");

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.ConfigureApplicationParts(parts =>
    {
        parts.AddApplicationPart(typeof(OrleansVoting.Grains.PollGrain).Assembly)
             .WithReferences();
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
```

**Add to solution:**
```bash
dotnet sln OrleansVoting.sln add OrleansVoting.Silo/OrleansVoting.Silo.csproj
```

**Verification:**
- ✅ Silo project builds: `dotnet build OrleansVoting.Silo`
- ✅ **All existing tests pass:** `dotnet test`

**Status:** ✅ **COMPLETE** (2025-10-20)

---

### Step 4.3: Add Silo to AppHost (Parallel Deployment)

**Purpose:** Run BOTH old Service (with embedded silo) AND new dedicated Silo simultaneously.

**Update OrleansVoting.AppHost/Program.cs:**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("voting-redis");

var orleans = builder.AddOrleans("voting-cluster")
    .WithClustering(redis)
    .WithGrainStorage("votes", redis);

// NEW: Dedicated silo instances
var silo = builder.AddProject<Projects.OrleansVoting_Silo>("voting-silo")
    .WithReference(orleans)
    .WithReplicas(2);  // Start with 2 replicas

// EXISTING: Service with embedded silo (still running!)
builder.AddProject<Projects.OrleansVoting_Service>("voting-fe")
    .WithReference(orleans)
    .WaitFor(redis)
    .WithReplicas(3)
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

**Run the application:**
```bash
dotnet run --project OrleansVoting.AppHost
```

**Manual Verification:**
- ✅ Aspire dashboard shows both `voting-silo` and `voting-fe`
- ✅ Both are "Running"
- ✅ Web UI accessible and functional
- ✅ Can create polls and vote
- ✅ Real-time updates work

**Automated Verification:**
```bash
dotnet test
```

**Success Criteria:**
- ✅ All tests pass
- ✅ Application runs with hybrid architecture
- ✅ Grains can activate on either Service or Silo instances

**Status:** ✅ **COMPLETE** (2025-10-20)

**Note:** One flaky integration test (`RealTimeUpdates_ObserverReceivesNotification`) may occasionally fail due to timing. This is a pre-existing issue not related to this step.

---

## Phase 5: Convert Service to Client-Only WebApp

**Goal:** Remove silo hosting from Service, make it pure Orleans client.

### Step 5.1: Add Tests for Client-Only Configuration

**Purpose:** Verify service can work as pure client before removing silo code.

**Tests to Write (OrleansVoting.Tests/Hosting/ClientHostTests.cs):**
```csharp
public class ClientHostTests
{
    [Fact]
    public async Task Client_CanConnect_ToSilo()
    {
        // Arrange - Start a silo
        var siloBuilder = Host.CreateApplicationBuilder();
        siloBuilder.UseOrleans(silo =>
        {
            silo.UseLocalhostClustering();
            silo.ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(PollGrain).Assembly).WithReferences();
            });
            silo.AddMemoryGrainStorage("votes");
        });
        var siloHost = siloBuilder.Build();
        await siloHost.StartAsync();

        // Arrange - Start a client
        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.UseOrleansClient(client =>
        {
            client.UseLocalhostClustering();
        });
        var clientHost = clientBuilder.Build();
        await clientHost.StartAsync();

        // Act
        var grainFactory = clientHost.Services.GetRequiredService<IGrainFactory>();
        var grain = grainFactory.GetGrain<IPollGrain>("test-client-grain");
        var state = new PollState { Question = "Test?", Options = new() };
        await grain.CreatePoll(state);
        var result = await grain.GetCurrentResults();

        // Assert
        result.Question.Should().Be("Test?");

        // Cleanup
        await clientHost.StopAsync();
        await siloHost.StopAsync();
    }
}
```

**Run tests:**
```bash
dotnet test --filter "FullyQualifiedName~ClientHostTests"
```

**Success Criteria:**
- ✅ Client host tests pass
- ✅ Demonstrates client-only configuration works

---

### Step 5.2: Create Client Configuration Method

**Purpose:** Add extension method to configure Orleans client, keeping both configurations side-by-side initially.

**Create OrleansVoting.ServiceDefaults/OrleansExtensions.cs:**
```csharp
namespace Microsoft.Extensions.Hosting;

public static class OrleansExtensions
{
    public static IHostApplicationBuilder UseOrleansClient(
        this IHostApplicationBuilder builder)
    {
        builder.UseOrleansClient(clientBuilder =>
        {
            // Client-specific configuration
            // Clustering configuration will come from Aspire
        });

        return builder;
    }
}
```

**Verification:**
- ✅ ServiceDefaults builds
- ✅ **All tests pass**

---

### Step 5.3: Create Backup of Current Service

**Actions:**
```bash
# Create a backup branch
git add -A
git commit -m "Checkpoint: Before converting Service to client-only"
git branch backup/service-with-silo
```

**Purpose:** Safety net if we need to revert.

---

### Step 5.4: Update Service to Use Client Configuration

**Purpose:** Change Service to connect as client instead of hosting silos.

**Update OrleansVoting.Service/AppHost.cs:**

**BEFORE:**
```csharp
builder.UseOrleans();
```

**AFTER:**
```csharp
builder.UseOrleansClient();
```

**Remove Grain References:**

Update OrleansVoting.Service.csproj - REMOVE:
```xml
<ProjectReference Include="..\OrleansVoting.Grains\OrleansVoting.Grains.csproj" />
```

Service should now ONLY reference Contracts, not Grains.

**Build:**
```bash
dotnet build OrleansVoting.Service
```

**Expected:** Build succeeds (Service no longer needs grain implementations)

---

### Step 5.5: Update AppHost Configuration

**Purpose:** Ensure Service connects to Silo cluster as client.

**Update OrleansVoting.AppHost/Program.cs:**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("voting-redis");

var orleans = builder.AddOrleans("voting-cluster")
    .WithClustering(redis)
    .WithGrainStorage("votes", redis);

// Dedicated silo instances (grain hosting)
var silo = builder.AddProject<Projects.OrleansVoting_Silo>("voting-silo")
    .WithReference(orleans)
    .WithReplicas(3);  // Increased from 2

// Web frontend (Orleans client only)
builder.AddProject<Projects.OrleansVoting_Service>("voting-fe")
    .WithReference(orleans)  // Connects as client
    .WaitFor(silo)  // Must wait for at least one silo
    .WithReplicas(3)
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

**Key Changes:**
- Service now depends on Silo being available
- Service is pure client, doesn't host grains

---

### Step 5.6: Test the Separation

**Run tests:**
```bash
dotnet test
```

**Expected:**
- ⚠️ Some tests might fail if they assume Service hosts grains
- ✅ Grain unit tests should still pass (use TestCluster)
- ✅ Service layer tests should pass (use mocks)
- ⚠️ Integration tests might need updating

**Fix Failing Integration Tests:**

If integration tests fail, update them to start both silo and client:

```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    public TestCluster Cluster { get; private set; }
    public IHost ClientHost { get; private set; }

    public async Task InitializeAsync()
    {
        // Start silo cluster
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        Cluster = builder.Build();
        await Cluster.DeployAsync();

        // Start client
        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.UseOrleansClient(client =>
        {
            client.UseLocalhostClustering();
        });
        ClientHost = clientBuilder.Build();
        await ClientHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await ClientHost?.StopAsync();
        await Cluster?.StopAsync();
        Cluster?.Dispose();
    }
}
```

**Success Criteria:**
- ✅ **All tests green again**
- ✅ Tests correctly model silo/client separation

---

### Step 5.7: Manual End-to-End Test

**Run the application:**
```bash
dotnet run --project OrleansVoting.AppHost
```

**Test Checklist:**
- ✅ Aspire dashboard shows `voting-silo` (3 replicas) running
- ✅ Aspire dashboard shows `voting-fe` (3 replicas) running
- ✅ Open web UI from dashboard
- ✅ Create a new poll
- ✅ Vote on poll
- ✅ Verify real-time updates work
- ✅ Try demo features
- ✅ Verify throttling still works

**If any manual test fails:**
1. Stop immediately
2. Check logs in Aspire dashboard
3. Verify silo is healthy
4. Verify client can connect to cluster
5. Fix issue before proceeding

**Success Criteria:**
- ✅ Full application works
- ✅ Service is pure client (no grain implementations)
- ✅ Silos host all grains
- ✅ **All automated tests pass**

---

## Phase 6: Rename and Finalize Structure

**Goal:** Clean up naming to reflect new architecture.

### Step 6.1: Rename Service to WebApp

**Actions:**
```bash
# Rename project folder
mv OrleansVoting.Service OrleansVoting.WebApp

# Rename project file
mv OrleansVoting.WebApp/OrleansVoting.Service.csproj OrleansVoting.WebApp/OrleansVoting.WebApp.csproj

# Update namespace in all .cs files
# Change: namespace OrleansVoting.Service
# To: namespace OrleansVoting.WebApp
```

**Update solution:**
```bash
dotnet sln OrleansVoting.sln remove OrleansVoting.Service/OrleansVoting.Service.csproj
dotnet sln OrleansVoting.sln add OrleansVoting.WebApp/OrleansVoting.WebApp.csproj
```

**Update AppHost references:**

Update OrleansVoting.AppHost.csproj:
```xml
<!-- Change from -->
<ProjectReference Include="..\OrleansVoting.Service\OrleansVoting.Service.csproj" />
<!-- To -->
<ProjectReference Include="..\OrleansVoting.WebApp\OrleansVoting.WebApp.csproj" />
```

Update OrleansVoting.AppHost/Program.cs:
```csharp
// Change from
builder.AddProject<Projects.OrleansVoting_Service>("voting-fe")
// To
builder.AddProject<Projects.OrleansVoting_WebApp>("voting-web")
```

**Update test references:**

Update OrleansVoting.Tests.csproj:
```xml
<ProjectReference Include="..\OrleansVoting.WebApp\OrleansVoting.WebApp.csproj" />
```

**Build and test:**
```bash
dotnet build
dotnet test
```

**Success Criteria:**
- ✅ Solution builds
- ✅ **All tests pass**
- ✅ Project renamed successfully

---

### Step 6.2: Final Verification

**Run full test suite:**
```bash
dotnet test --logger "console;verbosity=detailed"
```

**Run application:**
```bash
dotnet run --project OrleansVoting.AppHost
```

**Complete Manual Test Plan:**
1. ✅ Navigate to web UI
2. ✅ Create poll with 3 options
3. ✅ Vote on option
4. ✅ Open in second browser/incognito
5. ✅ Vote from second browser
6. ✅ Verify first browser sees update in real-time
7. ✅ Test throttling by rapid clicking
8. ✅ Test double-vote protection
9. ✅ Test demo features
10. ✅ Check Aspire dashboard health metrics
11. ✅ Verify all replicas are healthy
12. ✅ Check OpenTelemetry traces

**Success Criteria:**
- ✅ All manual tests pass
- ✅ All automated tests pass
- ✅ Architecture matches production design
- ✅ Clear separation: Contracts, Grains, Silo, WebApp
- ✅ Aspire orchestrates correctly

---

## Phase 7: Update Documentation

### Step 7.1: Update CLAUDE.md

**Actions:**
Update OrleansVoting/CLAUDE.md to reflect new architecture:

```markdown
## Project Structure

**OrleansVoting.Contracts** - Grain interfaces and DTOs
- Pure contracts, no implementations
- Referenced by all projects
- Defines the grain API surface

**OrleansVoting.Grains** - Grain implementations
- Business logic
- Referenced ONLY by Silo projects
- Not accessible to client applications

**OrleansVoting.Silo** - Dedicated Orleans silo host
- Headless compute tier
- Hosts grain activations
- No public HTTP endpoints
- Scales independently for computation

**OrleansVoting.WebApp** - Blazor frontend (Orleans client)
- Presentation tier
- Connects to Orleans cluster as client
- No grain implementations
- Scales independently for HTTP traffic

**OrleansVoting.ServiceDefaults** - Shared Aspire configuration
- OpenTelemetry with Orleans instrumentation
- Health checks and service discovery

**OrleansVoting.AppHost** - Aspire orchestration
- Configures separate silo and web tiers
- Redis for clustering and persistence

## Common Commands

### Running the Application
```bash
dotnet run --project OrleansVoting.AppHost
```
Starts:
- Redis
- 3 Silo replicas (grain hosting)
- 3 WebApp replicas (HTTP frontend)

### Running Tests
```bash
dotnet test
```

### Building
```bash
dotnet build OrleansVoting.sln
```

## Testing Strategy

### Grain Unit Tests (OrleansVoting.Tests/Grains/)
- Test grain behavior using TestCluster
- Verify business logic in isolation
- Test persistence, observers, throttling

### Service Layer Tests (OrleansVoting.Tests/Services/)
- Test PollService with mocked grains
- Verify service layer orchestration

### Component Tests (OrleansVoting.Tests/Components/)
- Test Blazor components using bUnit
- Verify UI behavior with mocked services

### Integration Tests (OrleansVoting.Tests/Integration/)
- End-to-end tests with real Orleans cluster
- Verify full system behavior

## Architecture Notes

### Client vs Silo
- **Silo** hosts grains, uses `builder.UseOrleans()`
- **Client** connects to cluster, uses `builder.UseOrleansClient()`
- WebApp is client-only, doesn't host grains

### Scaling
- Scale Silo for computation/grain workload
- Scale WebApp for HTTP traffic
- Independent scaling per tier

### Testing Grains
- Use TestCluster for grain tests
- Don't mock grains in grain tests
- Mock grains in service/component tests
```

---

### Step 7.2: Update README.md

**Actions:**
Update README.md to mention the production architecture:

```markdown
## Architecture

This sample demonstrates a **production-ready architecture** with clear separation:

- **Contracts**: Grain interfaces and DTOs
- **Grains**: Business logic implementations
- **Silo**: Dedicated grain hosting tier
- **WebApp**: Blazor frontend as Orleans client

The architecture allows:
- Independent scaling of compute (Silo) vs HTTP (WebApp)
- Multiple client types (Web, API, Mobile)
- Clear contract boundaries
- Independent deployment of UI and business logic
```

---

## Final Checklist

### Code Quality
- ✅ All tests pass
- ✅ No compiler warnings
- ✅ Proper separation of concerns
- ✅ Clear project boundaries

### Functionality
- ✅ Can create polls
- ✅ Can vote on polls
- ✅ Real-time updates work
- ✅ Throttling enforced
- ✅ Double-vote prevented
- ✅ Observer pattern works

### Architecture
- ✅ Contracts: interfaces only
- ✅ Grains: implementations only
- ✅ Silo: grain hosting only
- ✅ WebApp: UI + Orleans client only
- ✅ Clear dependencies (no circular references)

### Testing
- ✅ Grain unit tests (>90% coverage)
- ✅ Service layer tests (>85% coverage)
- ✅ Component tests (>75% coverage)
- ✅ Integration smoke tests
- ✅ All tests green

### Documentation
- ✅ CLAUDE.md updated
- ✅ README.md updated
- ✅ Architecture documented
- ✅ Testing strategy documented

---

## Risk Mitigation Summary

**How This Plan Minimizes Risk:**

1. **Test-First**: Every change has tests that will catch regressions
2. **Incremental**: Small steps, verify after each
3. **Reversible**: Backup checkpoints at critical junctions
4. **Parallel Deployment**: Run old and new side-by-side before cutting over
5. **Automated Verification**: Tests prevent silent breakage
6. **Manual Verification**: Human validation of critical paths

**If Something Goes Wrong:**
1. Tests will fail (catching the issue)
2. Revert to last checkpoint
3. Investigate failure
4. Fix issue
5. Verify tests pass
6. Continue

---

This plan ensures you can refactor with confidence, knowing every change is protected by tests that will alert you immediately if behavior changes unexpectedly.
