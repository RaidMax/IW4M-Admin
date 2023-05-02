namespace Data.Models.Zombie;

public class ZombieAggregateClientStat : ZombieClientStat
{
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

    #endregion
    
    public double RankingMetric { get; set; }
}
