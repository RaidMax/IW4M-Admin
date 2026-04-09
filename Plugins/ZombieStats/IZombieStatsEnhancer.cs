using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Data.Models.Zombie;
using IW4MAdmin.Plugins.ZombieStats.States;

namespace IW4MAdmin.Plugins.ZombieStats;

/// <summary>
/// Optional interface for premium zombie stats calculations.
/// When implemented and registered in DI, enables advanced analytics
/// (skill scoring, rolling averages, rich webfront metrics).
/// The free plugin works without this — basic stat tracking continues normally.
/// </summary>
public interface IZombieStatsEnhancer
{
    /// <summary>
    /// Called after basic round stats are rolled up into match/lifetime aggregates.
    /// Computes rolling averages, personal records, and other derived metrics.
    /// </summary>
    void OnRoundDataAggregated(MatchState matchState, RoundState roundState,
        ZombieMatchClientStat matchStat, ZombieAggregateClientStat lifetimeStat);

    /// <summary>
    /// Returns the skill calculation function for zombie clients.
    /// When not available, a no-op function is used (existing skill unchanged).
    /// </summary>
    Func<EFClient, EFClientStatistics, double> GetSkillCalculation();

    /// <summary>
    /// Provides advanced webfront metrics (percentages, averages, quit rate, stat tags)
    /// for the advanced stats page. Registered as a CustomStatsMetrics delegate.
    /// </summary>
    Task GetAdvancedStatsMetrics(Dictionary<int, List<EFMeta>> meta,
        long? serverId, string performanceBucketCode, bool isTopStats);
}
