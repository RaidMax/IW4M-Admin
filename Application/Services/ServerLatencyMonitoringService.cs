using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Application.Services;

/// <summary>
/// Per-server service that measures RCON round-trip time and log pipeline latency.
/// Owns the <see cref="ServerLatencyMetrics"/> instance. IW4MServer proxies access to it.
/// </summary>
public class ServerLatencyMonitoringService(Server server, ApplicationConfiguration appConfig, ILogger logger)
{
    private const string DvarProbe = "sv_iw4madmin_probe";
    private const string DvarLatencyProbe = "sv_iw4madmin_latencyprobe";
    private static readonly TimeSpan StaleProbeThreshold = TimeSpan.FromSeconds(30);
    private const double ProbeJitterFraction = 0.2;
    // Consecutive probes that RCon accepted but whose echo never came back through the game
    // log before going stale. Enough of them in a row means the log isn't reaching the parser.
    private const int UnansweredProbeWarnThreshold = 5;

    private readonly ConcurrentDictionary<string, DateTime> _pendingProbes = new();
    private readonly Random _jitterRng = new();

    // Guards _probeTimer and _probingStopped. The shutdown callback disposes the timer
    // while a probe may still be in flight; re-arming (Change) or creating the timer must
    // never race that dispose, or ObjectDisposedException escapes the async void callback.
    private readonly object _probeTimerLock = new();
    private Timer _probeTimer;
    private bool _probingStopped;
    private bool _gscDetected;
    private int _unansweredProbes;
    private bool _unansweredProbeWarned;

    public ServerLatencyMetrics LatencyMetrics { get; } = new(appConfig.LatencyEmaAlpha);

    public void Start(CancellationToken cancellationToken)
    {
        IGameServerEventSubscriptions.ServerStatusReceived += OnServerStatusReceived;

        if (appConfig.LatencyProbeIntervalMs > 0)
        {
            // GSC presence is determined by DetectGscCompanionAsync probing
            // sv_iw4madmin_latencyprobe directly — no pre-gate required, and
            // any pre-gate based on legacy flags (e.g. sv_customcallbacks)
            // produces false negatives on games that don't use them.
            IGameEventSubscriptions.ScriptEventTriggered += OnScriptEventTriggered;
            IGameEventSubscriptions.MatchStarted += OnMatchStarted;

            // Initial detection attempt after a short delay to allow GSC to initialize
            _ = new Timer(OnInitialDetection, null, appConfig.LatencyProbeIntervalMs, Timeout.Infinite);
        }

        cancellationToken.Register(() =>
        {
            lock (_probeTimerLock)
            {
                _probingStopped = true;
                _probeTimer?.Dispose();
                _probeTimer = null;
            }

            IGameServerEventSubscriptions.ServerStatusReceived -= OnServerStatusReceived;

            if (appConfig.LatencyProbeIntervalMs > 0)
            {
                IGameEventSubscriptions.ScriptEventTriggered -= OnScriptEventTriggered;
                IGameEventSubscriptions.MatchStarted -= OnMatchStarted;
            }

            _pendingProbes.Clear();
        });
    }

    private Task OnServerStatusReceived(ServerStatusReceiveEvent statusEvent, CancellationToken token)
    {
        if (server.RemoteConnection?.LastRtt is { } rtt)
        {
            LatencyMetrics.RecordRconRtt(rtt.TotalMilliseconds);
        }

        return Task.CompletedTask;
    }

    private Task OnScriptEventTriggered(GameScriptEvent scriptEvent, CancellationToken token)
    {
        if (scriptEvent is not LatencyProbeScriptEvent probe)
        {
            return Task.CompletedTask;
        }

        if (scriptEvent.Owner?.Id != server.Id)
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrEmpty(probe.ProbeId) && _pendingProbes.TryRemove(probe.ProbeId, out var sendTime))
        {
            Interlocked.Exchange(ref _unansweredProbes, 0);
            if (_unansweredProbeWarned)
            {
                _unansweredProbeWarned = false;
                using (LogContext.PushProperty("Server", server.Id))
                {
                    logger.LogInformation("Latency probe echoes are arriving through the game log again");
                }
            }

            var totalMs = (DateTime.UtcNow - sendTime).TotalMilliseconds;

            // GameLogIngestMs is the path a natural log line takes — game-write →
            // GLS poll → IW4MAdmin parse — and explicitly does NOT include the RCon
            // out-leg the probe used to start the clock. Subtract one-way RCon delivery
            // (rtt/2) from the measured total. If RCon RTT isn't established yet, skip
            // this sample rather than record an inflated number; EMA recovers on the
            // next probe once RTT samples accumulate.
            var rtt = LatencyMetrics.RconRoundTripMs;
            if (rtt is null)
            {
                using (LogContext.PushProperty("Server", server.Id))
                {
                    logger.LogDebug("Log probe {ProbeId} dropped: RCon RTT not yet stable", probe.ProbeId);
                }
                return Task.CompletedTask;
            }

            var pipelineMs = Math.Max(0, totalMs - rtt.Value / 2.0);
            LatencyMetrics.RecordLogProbeLatency(pipelineMs);

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogDebug("Log probe {ProbeId} pipeline: {PipelineMs:F1}ms (total {TotalMs:F1}ms - rtt/2 {HalfRtt:F1}ms)",
                    probe.ProbeId, pipelineMs, totalMs, rtt.Value / 2.0);
            }
        }

        return Task.CompletedTask;
    }

    private async Task OnMatchStarted(MatchStartEvent matchStartEvent, CancellationToken token)
    {
        if (matchStartEvent.Owner?.Id != server.Id)
        {
            return;
        }

        await TryDetectAndStartProbing();
    }

    private async void OnInitialDetection(object state)
    {
        try
        {
            await TryDetectAndStartProbing();
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogWarning(ex, "Error during initial GSC latency probe detection");
            }
        }
    }

    private async Task TryDetectAndStartProbing()
    {
        if (_gscDetected)
        {
            return;
        }

        await DetectGscCompanionAsync();

        if (!_gscDetected)
        {
            return;
        }

        lock (_probeTimerLock)
        {
            if (_probingStopped || _probeTimer is not null)
            {
                return;
            }

            // One-shot timer; OnProbeTimerElapsed re-arms with a jittered delay each
            // cycle so probes don't phase-lock to the GameLogReader poll cycle.
            _probeTimer = new Timer(OnProbeTimerElapsed, null, NextJitteredDelayMs(), Timeout.Infinite);
        }

        using (LogContext.PushProperty("Server", server.Id))
        {
            logger.LogInformation("Latency probe started (interval: {Interval}ms ±{JitterPct:P0})",
                appConfig.LatencyProbeIntervalMs, ProbeJitterFraction);
        }
    }

    private async void OnProbeTimerElapsed(object state)
    {
        try
        {
            CleanStalePendingProbes();
            await SendProbeAsync();
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogWarning(ex, "Error in latency probe timer");
            }
        }
        finally
        {
            // Re-arm only while probing is live; the shutdown callback disposes and nulls
            // the timer under the same lock, so Change can never see a disposed timer.
            lock (_probeTimerLock)
            {
                if (!_probingStopped)
                {
                    _probeTimer?.Change(NextJitteredDelayMs(), Timeout.Infinite);
                }
            }
        }
    }

    private int NextJitteredDelayMs()
    {
        var basePeriod = appConfig.LatencyProbeIntervalMs;
        var jitterRange = (int)(basePeriod * ProbeJitterFraction);
        // uniform offset in [-jitterRange, +jitterRange]
        var offset = _jitterRng.Next(-jitterRange, jitterRange + 1);
        return Math.Max(1, basePeriod + offset);
    }

    private async Task SendProbeAsync()
    {
        var probeId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            // T1 anchored to the actual UDP send moment via onPacketSent callback —
            // strips C#-side queue/flood-protect/retry pollution from the measurement.
            // TryAdd ensures retries (which re-fire the callback) don't overwrite the
            // first send time; GSC echoes on whichever attempt arrives first.
            await server.SetDvarAsync(DvarProbe, probeId, server.Manager.CancellationToken,
                onPacketSent: sentAt => _pendingProbes.TryAdd(probeId, sentAt));

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogDebug("Sent latency probe {ProbeId}", probeId);
            }
        }
        catch (Exception ex)
        {
            _pendingProbes.TryRemove(probeId, out _);

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogWarning(ex, "Failed to send latency probe");
            }
        }
    }

    private async Task DetectGscCompanionAsync()
    {
        try
        {
            var dvar = await server.GetDvarAsync(DvarLatencyProbe, "0");
            _gscDetected = dvar.Value == "1";

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogDebug("GSC latency probe companion detected: {Detected}", _gscDetected);
            }
        }
        catch (Exception ex)
        {
            _gscDetected = false;

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogWarning(ex, "Could not detect GSC latency probe companion");
            }
        }
    }

    private void CleanStalePendingProbes()
    {
        var cutoff = DateTime.UtcNow - StaleProbeThreshold;
        var snapshot = _pendingProbes.ToArray();

        foreach (var kvp in snapshot)
        {
            if (kvp.Value < cutoff && _pendingProbes.TryRemove(kvp.Key, out _))
            {
                Interlocked.Increment(ref _unansweredProbes);
            }
        }

        if (_unansweredProbeWarned || Volatile.Read(ref _unansweredProbes) < UnansweredProbeWarnThreshold)
        {
            return;
        }

        _unansweredProbeWarned = true;
        using (LogContext.PushProperty("Server", server.Id))
        {
            // The GSC answered the RCon detection, so the script runs; its log lines just never
            // get parsed. Check g_log / the game log path and any log forwarder for this server.
            logger.LogWarning(
                "The last {Count} latency probes were sent over RCon but none came back through the game log. " +
                "Game script events are not being read for this server; check its game log path and log forwarding",
                _unansweredProbes);
        }
    }
}
