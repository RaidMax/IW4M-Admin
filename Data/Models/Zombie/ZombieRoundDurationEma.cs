using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client.Stats.Reference;

namespace Data.Models.Zombie;

public class ZombieRoundDurationEma
{
    public int MapId { get; set; }
    [ForeignKey(nameof(MapId))]
    public virtual EFMap Map { get; set; }

    public int RoundNumber { get; set; }
    public int PlayerCount { get; set; }
    public double EmaSeconds { get; set; }
    public long SampleCount { get; set; }
}
