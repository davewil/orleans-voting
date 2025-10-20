namespace OrleansVoting.Contracts.Grains;

public interface IPollWatcher : IGrainObserver
{
    void OnPollUpdated(PollState state);
}
