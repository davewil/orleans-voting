using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Utilities;
using OrleansVoting.Contracts.Grains;

namespace OrleansVoting.Grains;

public class PollGrain(
    [PersistentState(stateName: "pollState", storageName: "votes")] IPersistentState<PollState> state,
    ILogger<ObserverManager<IPollWatcher>> pollLogger,
    ILogger<PollGrain> logger) : Grain, IPollGrain
{
    private readonly ObserverManager<IPollWatcher> _pollWatchers = new(TimeSpan.FromMinutes(1), pollLogger);

    public Task<PollState> GetCurrentResults()
    {
        // Validate grain state before returning
        if (state.State.Options is null)
        {
            throw new InvalidOperationException("Poll state is corrupted - options are null");
        }

        logger.LogDebug(
            "Poll {PollId}: Returning results - {TotalVotes} total votes across {OptionCount} options",
            this.GetPrimaryKeyString(),
            state.State.Options.Sum(o => o.Votes),
            state.State.Options.Count);

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

        logger.LogInformation(
            "Creating poll '{Question}' with {OptionCount} options for poll ID {PollId}",
            initialState.Question,
            initialState.Options.Count,
            this.GetPrimaryKeyString());

        // Set the state and persist it
        state.State = initialState;
        await state.WriteStateAsync();

        logger.LogInformation("Poll {PollId} created successfully", this.GetPrimaryKeyString());
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
        logger.LogInformation(
            "Poll {PollId}: Adding vote for option {OptionIndex} '{OptionText}' (currently {VoteCount} votes)",
            this.GetPrimaryKeyString(),
            optionId,
            option,
            votes);

        options[optionId] = (option, votes + 1);
        await state.WriteStateAsync();

        logger.LogInformation(
            "Poll {PollId}: Vote saved. Option '{OptionText}' now has {NewVoteCount} votes. Total poll votes: {TotalVotes}",
            this.GetPrimaryKeyString(),
            option,
            votes + 1,
            options.Sum(o => o.Votes));

        // Notify the watchers.
        var watcherCount = _pollWatchers.Count;
        if (watcherCount > 0)
        {
            logger.LogInformation(
                "Poll {PollId}: Notifying {WatcherCount} observers of vote update",
                this.GetPrimaryKeyString(),
                watcherCount);
        }
        _pollWatchers.Notify(watcher => watcher.OnPollUpdated(state.State));

        return state.State;
    }

    public Task StartWatching(IPollWatcher watcher)
    {
        _pollWatchers.Subscribe(watcher, watcher);
        logger.LogInformation(
            "Poll {PollId}: Observer subscribed. Total watchers: {WatcherCount}",
            this.GetPrimaryKeyString(),
            _pollWatchers.Count);
        return Task.CompletedTask;
    }

    public Task StopWatching(IPollWatcher watcher)
    {
        _pollWatchers.Unsubscribe(watcher);
        logger.LogInformation(
            "Poll {PollId}: Observer unsubscribed. Remaining watchers: {WatcherCount}",
            this.GetPrimaryKeyString(),
            _pollWatchers.Count);
        return Task.CompletedTask;
    }
}
