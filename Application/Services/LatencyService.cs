using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Application.Services;

/// <summary>
/// Per-server service that measures RCON round-trip time and log pipeline latency.
/// Subscribes to existing events — IW4MServer only manages lifecycle.
/// </summary>
public class LatencyService(Server server, ApplicationConfiguration appConfig, ILogger logger)
{
    private readonly ConcurrentDictionary<string, DateTime> _pendingProbes = new();

    private CancellationTokenSource _cts;
    private Task _probeLoopTask;
    private bool _gscDetected;

    private static readonly TimeSpan StaleProbeThreshold = TimeSpan.FromSeconds(30);

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();

        // Always subscribe to RTT sampling (works for all servers)
        IGameServerEventSubscriptions.ServerStatusReceived += OnServerStatusReceived;

        // Subscribe to script events for probe responses
        IGameEventSubscriptions.ScriptEventTriggered += OnScriptEventTriggered;

        // Start probe loop if config allows — GSC detection is retried inside the loop
        if (appConfig.LatencyProbeIntervalMs > 0 && server.IsLegacyGameIntegrationEnabled)
        {
            _probeLoopTask = Task.Run(() => ProbeLoopAsync(_cts.Token));
        }
    }

    public async Task StopAsync()
    {
        IGameServerEventSubscriptions.ServerStatusReceived -= OnServerStatusReceived;
        IGameEventSubscriptions.ScriptEventTriggered -= OnScriptEventTriggered;

        if (_cts is not null)
        {
            await _cts.CancelAsync();

            if (_probeLoopTask is not null)
            {
                try
                {
                    await _probeLoopTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _cts.Dispose();
            _cts = null;
        }

        _pendingProbes.Clear();
    }

    private Task OnServerStatusReceived(ServerStatusReceiveEvent statusEvent, CancellationToken token)
    {
        // ServerStatusReceiveEvent doesn't carry a server reference,
        // but we can always read our server's latest RTT — it only changes after our server's poll.
        if (server.RemoteConnection?.LastRtt is { } rtt)
        {
            server.LatencyMetrics.RecordRconRtt(rtt.TotalMilliseconds);
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
            server.LatencyMetrics.RecordLogProbeLatency(latencyMs);

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogDebug("Log probe {ProbeId} latency: {LatencyMs:F1}ms", probe.ProbeId, latencyMs);
            }
        }

        return Task.CompletedTask;
    }

    private async Task ProbeLoopAsync(CancellationToken token)
    {
        // Wait for GSC companion to initialize — retry detection each interval
        while (!token.IsCancellationRequested && !_gscDetected)
        {
            await Task.Delay(appConfig.LatencyProbeIntervalMs, token);
            await DetectGscCompanionAsync();
        }

        if (_gscDetected)
        {
            server.LatencyMetrics.LogProbeEnabled = true;

            using (LogContext.PushProperty("Server", server.Id))
            {
                logger.LogInformation("Latency probe started (interval: {Interval}ms)", appConfig.LatencyProbeIntervalMs);
            }
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(appConfig.LatencyProbeIntervalMs, token);
                CleanStalePendingProbes();
                await SendProbeAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("Server", server.Id))
                {
                    logger.LogWarning(ex, "Error in latency probe loop");
                }
            }
        }
    }

    private async Task SendProbeAsync(CancellationToken token)
    {
        var probeId = Guid.NewGuid().ToString("N")[..8];
        _pendingProbes[probeId] = DateTime.UtcNow;

        try
        {
            await server.SetDvarAsync("sv_iw4madmin_probe", probeId, token);

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
                logger.LogDebug(ex, "Failed to send latency probe");
            }
        }
    }

    private async Task DetectGscCompanionAsync()
    {
        try
        {
            var dvar = await server.GetDvarAsync("sv_iw4madmin_latencyprobe", "0");
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
                logger.LogDebug(ex, "Could not detect GSC latency probe companion");
            }
        }
    }

    private void CleanStalePendingProbes()
    {
        var cutoff = DateTime.UtcNow - StaleProbeThreshold;

        foreach (var kvp in _pendingProbes.Where(kvp => kvp.Value < cutoff))
        {
            _pendingProbes.TryRemove(kvp.Key, out _);
        }
    }
}
