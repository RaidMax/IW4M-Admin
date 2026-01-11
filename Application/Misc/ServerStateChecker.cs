using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models.Client.Stats;
using Humanizer;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Application.Misc;

/// <summary>
/// Encapsulates fail-state detection logic for game servers
/// </summary>
public class ServerStateChecker(ApplicationConfiguration config, ILogger<ServerStateChecker> logger) : IServerStateChecker
{
    private readonly Dictionary<long, int> _previousPlayerScores = new();
    private readonly Dictionary<long, int> _previousPlayerPings = new();

    /// <summary>
    /// Indicates if stale checking is disabled due to unreliable ping data (all 0 or 999)
    /// </summary>
    private bool _pingDataUnreliable;

    /// <summary>
    /// The server this state checker is monitoring
    /// </summary>
    public Server Server { get; private set; }

    /// <summary>
    /// Last time any activity was detected (score changes, joins/leaves, stats events)
    /// </summary>
    public DateTime? LastActivity { get; private set; }

    /// <summary>
    /// Timestamp when fail-state was first detected (for logging/recovery)
    /// </summary>
    public DateTime? FailStateDetectedAt { get; private set; }

    /// <summary>
    /// Initialize the state checker for a specific server and subscribe to events
    /// </summary>
    public void Initialize(Server server)
    {
        Server = server ?? throw new ArgumentNullException(nameof(server));

        // Subscribe to stats events for fail-state detection
        IGameEventSubscriptions.ClientKilled += OnClientKilled;

        // Subscribe to client data update events for score change detection
        IGameServerEventSubscriptions.ClientDataUpdated += OnClientDataUpdated;

        // Subscribe to client state events for join/leave activity tracking
        IManagementEventSubscriptions.ClientStateInitialized += OnClientStateInitialized;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientStateDisposed;
    }

    /// <summary>
    /// Unsubscribes from all events to support dynamic server removal
    /// </summary>
    public void Dispose()
    {
        IGameEventSubscriptions.ClientKilled -= OnClientKilled;
        IGameServerEventSubscriptions.ClientDataUpdated -= OnClientDataUpdated;
        IManagementEventSubscriptions.ClientStateInitialized -= OnClientStateInitialized;
        IManagementEventSubscriptions.ClientStateDisposed -= OnClientStateDisposed;
    }

    private async Task OnClientKilled(ClientKillEvent killEvent, CancellationToken token)
    {
        if (!IsServerMatch(killEvent.Server))
        {
            return;
        }

        // Track stats activity for fail-state detection
        if (killEvent.Attacker?.CurrentServer == Server && !killEvent.Attacker.IsBot)
        {
            RecordActivity();
        }
    }

    private async Task OnClientDataUpdated(ClientDataUpdateEvent updateEvent, CancellationToken token)
    {
        if (!IsServerMatch(updateEvent.Server))
        {
            return;
        }

        // Process player updates to detect score changes
        var currentClients = Server.GetClientsAsList();
        ProcessPlayerUpdates(updateEvent.Clients, currentClients);
    }

    private async Task OnClientStateInitialized(ClientStateInitializeEvent stateEvent, CancellationToken token)
    {
        if (!IsServerMatch(stateEvent.Source as IGameServer))
        {
            return;
        }

        // Track join activity for fail-state detection
        if (!stateEvent.Client.IsBot)
        {
            RecordActivity();
        }
    }

    private async Task OnClientStateDisposed(ClientStateDisposeEvent stateEvent, CancellationToken token)
    {
        if (!IsServerMatch(stateEvent.Source as IGameServer))
        {
            return;
        }

        // Track leave activity for fail-state detection
        if (!stateEvent.Client.IsBot)
        {
            RecordActivity();
        }
    }

    /// <summary>
    /// Check if the event is from the server this checker is monitoring
    /// </summary>
    private bool IsServerMatch(IGameServer eventServer)
    {
        return Server != null && eventServer?.Id == Server.Id;
    }

    /// <summary>
    /// Records that activity has occurred (updates LastActivity timestamp)
    /// </summary>
    public void RecordActivity()
    {
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Determines if players should be disconnected due to recent fail-state detection.
    /// This encapsulates the grace period logic which may vary by game.
    /// </summary>
    public bool ShouldDisconnectPlayersOnFailState()
    {
        if (!FailStateDetectedAt.HasValue)
        {
            return false;
        }
        
        // Grace period before resuming normal polling - may need adjustment for specific games
        var gracePeriod = config?.FailStateGracePeriod ?? TimeSpan.FromSeconds(30);
        return (DateTime.UtcNow - FailStateDetectedAt.Value) < gracePeriod;
    }

    /// <summary>
    /// Computes whether the server is in a fail-state based on configuration and current player count
    /// </summary>
    public bool IsInErrorState()
    {
        if (Server == null)
        {
            return false;
        }
        
        var nonBotPlayerCount = Server.GetClientsAsList().Count(c => !c.IsBot);
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
    /// Processes player updates to detect score and ping changes, updates LastActivity accordingly
    /// </summary>
    public void ProcessPlayerUpdates(IEnumerable<EFClient> updatedClients, IEnumerable<EFClient> polledClients)
    {
        var clientList = updatedClients.ToList();

        // Check if all pings are 0 or 999 (unreliable ping data - e.g., WaW after weeks of uptime)
        if (clientList.Count > 0)
        {
            var allPingsUnreliable = clientList.All(c => c.Ping is 0 or 999);
            if (allPingsUnreliable != _pingDataUnreliable)
            {
                _pingDataUnreliable = allPingsUnreliable;
                logger.LogInformation("{Status}", _pingDataUnreliable
                    ? "Ping data unreliable (all 0 or 999) - ping variance check disabled for server"
                    : "Ping data now reliable - ping variance check enabled for server");
            }
        }

        // Check for score and ping changes
        foreach (var client in clientList)
        {
            var hasActivity = false;

            // Check score change
            if (_previousPlayerScores.TryGetValue(client.NetworkId, out var previousScore))
            {
                if (client.Score != previousScore)
                {
                    hasActivity = true;
                }
            }
            else
            {
                // New player - this is activity
                hasActivity = true;
            }

            // Check ping change (only if ping data is reliable)
            if (!_pingDataUnreliable && _previousPlayerPings.TryGetValue(client.NetworkId, out var previousPing))
            {
                // Ping change > 1 indicates real activity (frozen servers return exact same value)
                if (Math.Abs(client.Ping - previousPing) > 1)
                {
                    hasActivity = true;
                }
            }

            if (hasActivity)
            {
                RecordActivity();
                // Don't break - still need to update all stored values
            }

            _previousPlayerScores[client.NetworkId] = client.Score;
            _previousPlayerPings[client.NetworkId] = client.Ping;
        }

        // Remove data for disconnected players
        var currentNetworkIds = polledClients.Select(c => c.NetworkId).ToHashSet();
        var keysToRemove = _previousPlayerScores.Keys.Where(k => !currentNetworkIds.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            _previousPlayerScores.Remove(key);
            _previousPlayerPings.Remove(key);
        }
    }

    /// <summary>
    /// Checks and updates fail-state status, returns true if status changed
    /// </summary>
    public bool CheckAndUpdateFailState()
    {
        if (Server == null)
        {
            return false;
        }
        
        var currentClients = Server.GetClientsAsList();
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
        foreach (var client in currentClients.Where(c => !c.IsBot))
        {
            var stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY);
            if (stats != null && stats.LastActive > (now - threshold))
            {
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
        var shouldBeFailState = IsInErrorState();

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
