using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using SharedLibraryCore.Events.Game;

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Optional interface for premium zombie stats functionality.
/// When implemented and registered in DI, enables full zombie stat tracking,
/// skill scoring, rolling averages, and rich webfront metrics.
/// Without this, the free plugin only bridges zombie kills/damage/deaths
/// to the standard Stats plugin (K/D/Score like MP).
/// </summary>
public interface IZombieStatsEnhancer
{
    /// <summary>
    /// Called once on plugin load to initialize caches and DB state.
    /// </summary>
    Task Initialize();

    /// <summary>
    /// Process a parsed zombie game event (kills, deaths, downs, revives,
    /// perks, powerups, round data, stat updates, etc.).
    /// </summary>
    void ProcessEvent(GameEventV2 parsedEvent);

    /// <summary>
    /// Called when a client connects to a zombie server.
    /// Sets up match/round state, loads aggregate stats from DB.
    /// </summary>
    Task OnClientAuthorized(EFClient client, IGameServer server);

    /// <summary>
    /// Called when a client disconnects from a zombie server.
    /// Finalizes round state and cleans up tracking.
    /// </summary>
    Task OnClientDisposed(EFClient client, IGameServer server);

    /// <summary>
    /// Called when a new match starts on a zombie server.
    /// </summary>
    void OnMatchStarted(IGameServer server);

    /// <summary>
    /// Called when a match ends on a zombie server.
    /// </summary>
    void OnMatchEnded(IGameServer server);

    /// <summary>
    /// Persists all pending state changes to the database.
    /// </summary>
    Task UpdateState(CancellationToken token);

    /// <summary>
    /// Returns the skill calculation function for zombie clients.
    /// </summary>
    Func<EFClient, EFClientStatistics, double> GetSkillCalculation();

    /// <summary>
    /// Provides zombie-specific metrics for the top stats leaderboard page.
    /// </summary>
    Task GetTopStatsMetrics(Dictionary<int, List<EFMeta>> meta,
        long? serverId, string performanceBucketCode, bool isTopStats);

    /// <summary>
    /// Provides advanced zombie metrics for the player stats page.
    /// </summary>
    Task GetAdvancedStatsMetrics(Dictionary<int, List<EFMeta>> meta,
        long? serverId, string performanceBucketCode, bool isTopStats);
}
