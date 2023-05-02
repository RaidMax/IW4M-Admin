using System;

namespace Data.Models.Zombie;

public class ZombieRoundClientStat : ZombieClientStat
{
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public TimeSpan? TimeAlive { get; set; }
    public int RoundNumber { get; set; }
    public int Points { get; set; }
}
