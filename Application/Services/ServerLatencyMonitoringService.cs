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

    private readonly ConcurrentDictionary<string, DateTime> _pendingProbes = new();

    private Timer _probeTimer;
    private bool _gscDetected;

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
            _probeTimer?.Dispose();
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
            var latencyMs = (DateTime.UtcNow - sendTime).TotalMilliseconds;
            LatencyMetrics.RecordLogProbeLatency(latencyMs);

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogDebug("Log probe {ProbeId} latency: {LatencyMs:F1}ms", probe.ProbeId, latencyMs);
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

        if (_gscDetected && _probeTimer is null)
        {
            _probeTimer = new Timer(OnProbeTimerElapsed, null, appConfig.LatencyProbeIntervalMs,
                appConfig.LatencyProbeIntervalMs);

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogInformation("Latency probe started (interval: {Interval}ms)",
                    appConfig.LatencyProbeIntervalMs);
            }
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
    }

    private async Task SendProbeAsync()
    {
        var probeId = Guid.NewGuid().ToString("N")[..8];
        _pendingProbes[probeId] = DateTime.UtcNow;

        try
        {
            await server.SetDvarAsync(DvarProbe, probeId, server.Manager.CancellationToken);

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
            if (kvp.Value < cutoff)
            {
                _pendingProbes.TryRemove(kvp.Key, out _);
            }
        }
    }
}
