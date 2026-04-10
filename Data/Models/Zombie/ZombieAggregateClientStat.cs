using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Server;

namespace Data.Models.Zombie;

public class ZombieAggregateClientStat : ZombieClientStat
{
    public long? ServerId { get; set; }
    [ForeignKey(nameof(ServerId))]
    public EFServer Server { get; set; }

    #region Average

    public double AverageKillsPerDown { get; set; }
    public double AverageDowns { get; set; }
    public double AverageRevives { get; set; }
    public double HeadshotPercentage { get; set; }
    public double AlivePercentage { get; set; }
    public double AverageMelees { get; set; }
    public double AverageRoundReached { get; set; }
    public double AveragePoints { get; set; }

    #endregion

    #region Totals

    public int HighestRound { get; set; }
    public int TotalRoundsPlayed { get; set; }
    public int TotalMatchesPlayed { get; set; }
    public int TotalMatchesCompleted { get; set; }

    #endregion
}
