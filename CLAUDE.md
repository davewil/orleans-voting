# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Orleans Voting is a sample application demonstrating Orleans distributed actor framework integration with .NET Aspire orchestration. It implements a real-time voting application with persistence and clustering backed by Redis.

## Common Commands

### Running the Application
```bash
dotnet run --project OrleansVoting.AppHost
```
This starts the Aspire AppHost which orchestrates Redis and 3 replicas of the voting service.

### Building
```bash
dotnet build OrleansVoting.sln
```

### Restore Dependencies
```bash
dotnet restore
```

## Architecture

### Project Structure

**OrleansVoting.AppHost** - Aspire orchestration host
- Configures Redis for Orleans clustering and grain persistence
- Deploys 3 replicas of the voting service with load balancing
- Entry point: `AppHost.cs`

**OrleansVoting.Service** - Web frontend and Orleans silos
- ASP.NET Core with Blazor Server UI
- Hosts Orleans grains in each replica
- Entry point: `AppHost.cs` (not Program.cs)
- Uses `builder.UseOrleans()` extension to configure Orleans integration

**OrleansVoting.ServiceDefaults** - Shared Aspire configuration
- OpenTelemetry setup (metrics, tracing, logging) with Orleans-specific instrumentation
- Health checks and service discovery
- Applied via `builder.AddServiceDefaults()`

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
- **Connection**: Injected via `builder.AddKeyedRedisClient("voting-redis")` in Service project

### Key Orleans Patterns

- **Persistent State**: `[PersistentState]` attribute injects `IPersistentState<T>` for grain state
- **Observer Pattern**: `ObserverManager<IPollWatcher>` manages grain-to-client callbacks for live updates
- **Grain Interfaces**: Must inherit from `IGrainWithStringKey` or similar marker interface
- **Serialization**: Use `[GenerateSerializer]` and `[Id(n)]` attributes on state classes

### Aspire Integration Points

- `builder.AddServiceDefaults()` - applies telemetry, health checks, service discovery
- `builder.UseOrleans()` - configures Orleans from Aspire service configuration
- `builder.AddKeyedRedisClient()` - retrieves Redis connection from Aspire orchestration
- `app.MapDefaultEndpoints()` - exposes health check endpoints in development
