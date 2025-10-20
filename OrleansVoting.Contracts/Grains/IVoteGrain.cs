using Orleans;

namespace OrleansVoting.Contracts.Grains;

public interface IVoteGrain : IGrainWithIntegerKey
{
    Task<Dictionary<string, int>> Get();
    Task AddVote(string option);
    Task RemoveVote(string option);
}
