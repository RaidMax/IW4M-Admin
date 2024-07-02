using Data.Models.Zombie;

namespace IW4MAdmin.Plugins.ZombieStats.States;

public record RoundState
{
    public ZombieRoundClientStat PersistentClientRound { get; init; } = null!;
    public DateTimeOffset? DiedAt { get; set; }
    public int Hits { get; set; }
}
