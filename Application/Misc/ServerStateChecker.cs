using System;
using System.Collections.Generic;
using System.Linq;
using Data.Models.Client.Stats;
using Humanizer;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Misc;

/// <summary>
/// Encapsulates fail-state detection logic for game servers
/// </summary>
public class ServerStateChecker(ApplicationConfiguration config, ILogger<ServerStateChecker> logger) : IServerStateChecker
{
    private readonly Dictionary<long, int> _previousPlayerScores = new();

    /// <summary>
    /// Last time any activity was detected (score changes, joins/leaves, stats events)
    /// </summary>
    public DateTime? LastActivity { get; private set; }

    /// <summary>
    /// Timestamp when fail-state was first detected (for logging/recovery)
    /// </summary>
    public DateTime? FailStateDetectedAt { get; private set; }

    /// <summary>
    /// Records that activity has occurred (updates LastActivity timestamp)
    /// </summary>
    public void RecordActivity()
    {
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Computes whether the server is in a fail-state based on configuration and player count
    /// </summary>
    public bool IsErrorState(int nonBotPlayerCount)
    {
        if (nonBotPlayerCount < (config?.FailStateMinPlayers ?? 1))
        {
            return false;
        }

        if (!LastActivity.HasValue)
        {
            return false;
        }

        var threshold = config?.FailStateDetectionThreshold ?? TimeSpan.FromHours(2);
        return (DateTime.UtcNow - LastActivity.Value) > threshold;
    }

    /// <summary>
    /// Processes player updates to detect score changes and updates LastActivity accordingly
    /// </summary>
    public void ProcessPlayerUpdates(IEnumerable<EFClient> updatedClients, IEnumerable<EFClient> polledClients)
    {
        // Check for score changes
        foreach (var client in updatedClients)
        {
            if (_previousPlayerScores.TryGetValue(client.NetworkId, out var previousScore))
            {
                if (client.Score != previousScore)
                {
                    RecordActivity();
                    break;
                }
            }
            else
            {
                // New player - this is activity
                RecordActivity();
            }

            _previousPlayerScores[client.NetworkId] = client.Score;
        }

        // Remove scores for disconnected players
        var currentNetworkIds = polledClients.Select(c => c.NetworkId).ToHashSet();
        var keysToRemove = _previousPlayerScores.Keys.Where(k => !currentNetworkIds.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            _previousPlayerScores.Remove(key);
        }
    }

    /// <summary>
    /// Checks and updates fail-state status, returns true if status changed
    /// </summary>
    public bool CheckAndUpdateFailState(List<EFClient> currentClients)
    {
        var threshold = config?.FailStateDetectionThreshold ?? TimeSpan.FromHours(2);
        var minPlayers = config?.FailStateMinPlayers ?? 1;
        var now = DateTime.UtcNow;

        // Need at least minimum players to trigger fail-state detection
        var nonBotPlayers = currentClients.Count(c => !c.IsBot);
        if (nonBotPlayers < minPlayers)
        {
            // Empty or low-population server - reset fail-state if active
            if (FailStateDetectedAt.HasValue)
            {
                FailStateDetectedAt = null;
                return true;
            }

            return false;
        }

        // Check stats activity (LastActive from StatManager)
        var hasStatsActivity = false;
        foreach (var client in currentClients.Where(c => !c.IsBot))
        {
            var stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
            if (stats != null && stats.LastActive > (now - threshold))
            {
                hasStatsActivity = true;
                RecordActivity();
                break;
            }
        }

        // Initialize activity timestamp if not set
        if (!LastActivity.HasValue)
        {
            LastActivity = now;
        }

        // Determine if server is in fail-state
        var shouldBeFailState = IsErrorState(nonBotPlayers);

        if (shouldBeFailState && !FailStateDetectedAt.HasValue)
        {
            // Entering fail-state
            FailStateDetectedAt = now;
            var age = LastActivity.HasValue
                ? (now - LastActivity.Value).Humanize()
                : "unknown";
            logger.LogWarning(
                "Server detected in fail-state - no activity for {Age}. Players: {PlayerCount}",
                age,
                nonBotPlayers);
            return true;
        }

        if (!shouldBeFailState && FailStateDetectedAt.HasValue)
        {
            // Recovering from fail-state
            var duration = FailStateDetectedAt.HasValue
                ? (now - FailStateDetectedAt.Value).Humanize()
                : "unknown";
            logger.LogInformation("Server recovered from fail-state after {Duration}", duration);
            FailStateDetectedAt = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the duration the server has been in fail-state, or null if not in fail-state
    /// </summary>
    public TimeSpan? GetFailStateDuration()
    {
        if (!FailStateDetectedAt.HasValue)
        {
            return null;
        }

        return DateTime.UtcNow - FailStateDetectedAt.Value;
    }
}
