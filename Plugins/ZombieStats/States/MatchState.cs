using Data.Models.Client.Stats;
using Data.Models.Zombie;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ZombieStats.States;

public record MatchState(IGameServer Server, ZombieMatch PersistentMatch) 
{
    public Dictionary<long, RoundState> RoundStates { get; } = new();
    public Dictionary<long, ZombieMatchClientStat> PersistentMatchAggregateStats { get; } = new();
    public Dictionary<long, ZombieAggregateClientStat> PersistentLifetimeAggregateStats { get; } = new();
    public Dictionary<long, ZombieAggregateClientStat> PersistentLifetimeServerAggregateStats { get; } = new();
    public Dictionary<long, Dictionary<string, EFClientStatTagValue>> PersistentStatTagValues { get; } = new();
    public int RoundNumber { get; set; }
}
