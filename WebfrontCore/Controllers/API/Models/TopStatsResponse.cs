using IW4MAdmin.Plugins.Stats.Web.Dtos;

namespace WebfrontCore.Controllers.API.Models;

public class TopStatsResponse
{
    public List<TopStatsInfo> Players { get; set; } = [];
    public long TotalRankedClients { get; set; }

    /// <summary>
    /// Pass this value as the next request's <c>offset</c> to walk the leaderboard
    /// without re-visiting rejected rows. Server-side ranking history is filtered
    /// against the stats join, so a single page may consume more underlying rows
    /// than it returns; this advances by the actual rows consumed. Equal to
    /// <c>request.Offset + Players.Count</c> only when no rows were filtered.
    /// </summary>
    public int NextOffset { get; set; }
}
