using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Utilities;
using OrleansVoting.Contracts.Grains;

namespace OrleansVoting.Grains;

public class PollGrain(
    [PersistentState(stateName: "pollState", storageName: "votes")] IPersistentState<PollState> state,
    ILogger<ObserverManager<IPollWatcher>> pollLogger) : Grain, IPollGrain
{
    private readonly ObserverManager<IPollWatcher> _pollWatchers = new(TimeSpan.FromMinutes(1), pollLogger);

    public Task<PollState> GetCurrentResults()
    {
        // Validate grain state before returning
        if (state.State.Options is null)
        {
            throw new InvalidOperationException("Poll state is corrupted - options are null");
        }

        return Task.FromResult(state.State);
    }

    public async Task CreatePoll(PollState initialState)
    {
        // Validate the initial state
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(initialState.Options);

        if (string.IsNullOrWhiteSpace(initialState.Question))
        {
            throw new ArgumentException("Poll question cannot be empty", nameof(initialState));
        }

        if (initialState.Options.Count == 0)
        {
            throw new ArgumentException("Poll must have at least one option", nameof(initialState));
        }

        // Set the state and persist it
        state.State = initialState;
        await state.WriteStateAsync();
    }

    public async Task<PollState> AddVote(int optionId)
    {
        // Validate grain state
        if (state.State.Options is null)
        {
            throw new InvalidOperationException("Poll state is corrupted - options are null");
        }

        // Perform input validation
        var options = state.State.Options;
        if (optionId < 0 || optionId >= options.Count)
        {
            throw new KeyNotFoundException($"Invalid option {optionId}");
        }

        // Add the vote & persist the updated state.
        var (option, votes) = options[optionId];
        options[optionId] = (option, votes + 1);
        await state.WriteStateAsync();

        // Notify the watchers.
        _pollWatchers.Notify(watcher => watcher.OnPollUpdated(state.State));
        return state.State;
    }

    public Task StartWatching(IPollWatcher watcher)
    {
        _pollWatchers.Subscribe(watcher, watcher);
        return Task.CompletedTask;
    }

    public Task StopWatching(IPollWatcher watcher)
    {
        _pollWatchers.Unsubscribe(watcher);
        return Task.CompletedTask;
    }
}
