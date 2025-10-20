using OrleansVoting;
using OrleansVoting.Contracts.Grains;

namespace OrleansVoting.Tests.Fixtures;

public class TestPollWatcher : IPollWatcher
{
    public int UpdateCount { get; private set; }
    public PollState? LastState { get; private set; }

    public void OnPollUpdated(PollState state)
    {
        UpdateCount++;
        LastState = state;
    }
}
