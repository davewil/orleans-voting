# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Orleans Voting is a sample application demonstrating Orleans distributed actor framework integration with .NET Aspire orchestration. It implements a real-time voting application with persistence and clustering backed by Redis.

## Common Commands

### Running the Application
```bash
dotnet run --project OrleansVoting.AppHost
```
Starts:
- Redis
- 3 Silo replicas (grain hosting)
- 3 WebApp replicas (HTTP frontend)

### Building
```bash
dotnet build OrleansVoting.sln
```

### Running Tests
```bash
dotnet test
```

### Restore Dependencies
```bash
dotnet restore
```

## Architecture

### Project Structure

**OrleansVoting.Contracts** - Grain interfaces and DTOs
- Pure contracts, no implementations
- Referenced by all projects
- Defines the grain API surface
- Contains: `IPollGrain`, `IUserAgentGrain`, `IVoteGrain`, `IPollWatcher`, `PollState`, `ThrottlingException`

**OrleansVoting.Grains** - Grain implementations
- Business logic for all grains
- Referenced ONLY by Silo projects
- Not accessible to client applications
- Contains: `PollGrain`, `UserAgentGrain`, `VoteGrain`

**OrleansVoting.Silo** - Dedicated Orleans silo host
- Headless compute tier
- Hosts grain activations
- No public HTTP endpoints (only health checks)
- Scales independently for computation
- Entry point: `Program.cs`
- Uses `builder.UseOrleans()` to configure silo hosting

**OrleansVoting.WebApp** - Blazor frontend (Orleans client)
- Presentation tier
- ASP.NET Core with Blazor Server UI
- Connects to Orleans cluster as client (no grain hosting)
- Scales independently for HTTP traffic
- Entry point: `AppHost.cs`
- Uses `builder.UseOrleansClient()` to configure Orleans client

**OrleansVoting.ServiceDefaults** - Shared Aspire configuration
- OpenTelemetry setup (metrics, tracing, logging) with Orleans-specific instrumentation
- Health checks and service discovery
- Applied via `builder.AddServiceDefaults()`
- Contains `UseOrleansClient()` extension method for client configuration

**OrleansVoting.AppHost** - Aspire orchestration
- Configures separate silo and web tiers
- Redis for clustering and persistence
- Entry point: `AppHost.cs`
- Deploys 3 Silo replicas (grain hosting) + 3 WebApp replicas (HTTP frontend)

### Orleans Grain Model

The application uses three grain types to implement the voting system:

**UserAgentGrain** (`IUserAgentGrain`) - Per-client session management
- Keyed by client IP address
- Tracks which polls a user created/voted in to prevent double-voting
- Implements simple throttling (10 requests per 5 seconds with decay)
- Enforces limit of 5 polls per user

**PollGrain** (`IPollGrain`) - Individual poll state
- Keyed by poll ID (6-character GUID prefix)
- Persists poll question and vote counts using Redis storage (storage name: "votes")
- Implements observer pattern via `ObserverManager<IPollWatcher>` for real-time updates
- State type: `PollState` with `[GenerateSerializer]` for wire serialization

**VoteGrain** (`IVoteGrain`) - Alternative voting grain (appears unused by main app)
- Dictionary-based vote counting with Redis persistence

### Data Flow

1. Client requests go through `PollService` which wraps Orleans grain calls
2. `PollService.Initialize()` must be called with client IP to get the `UserAgentGrain`
3. Poll creation: `UserAgentGrain` → generates ID → `PollGrain.CreatePoll()`
4. Voting: `UserAgentGrain.AddVote()` → checks double-vote → `PollGrain.AddVote()`
5. Real-time updates: `PollService.WatchPoll()` subscribes to `PollGrain` observer notifications

### Redis Integration

- **Clustering**: Orleans silo discovery and membership managed via Redis (Aspire hosting)
- **Grain Storage**: Poll state persisted to Redis with provider name "votes"
- **Connection**: Injected via `builder.AddKeyedRedisClient("voting-redis")` in Silo and WebApp projects

### Key Orleans Patterns

- **Persistent State**: `[PersistentState]` attribute injects `IPersistentState<T>` for grain state
- **Observer Pattern**: `ObserverManager<IPollWatcher>` manages grain-to-client callbacks for live updates
- **Grain Interfaces**: Must inherit from `IGrainWithStringKey` or similar marker interface
- **Serialization**: Use `[GenerateSerializer]` and `[Id(n)]` attributes on state classes

### Aspire Integration Points

- `builder.AddServiceDefaults()` - applies telemetry, health checks, service discovery
- `builder.UseOrleans()` - configures Orleans silo hosting (Silo project only)
- `builder.UseOrleansClient()` - configures Orleans client (WebApp project)
- `builder.AddKeyedRedisClient()` - retrieves Redis connection from Aspire orchestration
- `app.MapDefaultEndpoints()` - exposes health check endpoints in development

## Testing Strategy

### Grain Unit Tests (OrleansVoting.Tests/Grains/)
- Test grain behavior using TestCluster
- Verify business logic in isolation
- Test persistence, observers, throttling
- Use real Orleans TestCluster (not mocks)

### Service Layer Tests (OrleansVoting.Tests/Services/)
- Test PollService with mocked grains
- Verify service layer orchestration
- Fast execution with Moq

### Component Tests (OrleansVoting.Tests.UI/Components/)
- Test Blazor components using bUnit
- Verify UI behavior with mocked services
- Test user interactions and error handling

### Integration Tests (OrleansVoting.Tests/Integration/)
- End-to-end tests with real Orleans cluster
- Verify full system behavior
- Test cross-grain interactions

### Hosting Tests (OrleansVoting.Tests/Hosting/)
- Verify silo can start and host grains
- Verify client can connect to cluster
- Test separate silo/client architecture

## Architecture Notes

### Client vs Silo
- **Silo** hosts grains, uses `builder.UseOrleans()`
- **Client** connects to cluster, uses `builder.UseOrleansClient()`
- WebApp is client-only, doesn't host grains
- Grains project only referenced by Silo, not by WebApp

### Scaling
- Scale Silo for computation/grain workload
- Scale WebApp for HTTP traffic
- Independent scaling per tier
- Both tiers connect to same Redis cluster

### Testing Grains
- Use TestCluster for grain tests
- Don't mock grains in grain tests
- Mock grains in service/component tests
- Integration tests verify silo/client separation
