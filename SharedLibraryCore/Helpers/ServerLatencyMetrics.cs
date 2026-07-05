using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedLibraryCore.Helpers;

public class ServerLatencyMetrics(double alpha = 0.3)
{
    private readonly Lock _lock = new();
    private readonly double _alpha = Math.Clamp(alpha, 0.01, 1.0);
    private int _rconSampleCount;
    private double _rconRtt;
    private const int MinRconSamples = 3;

    private const int LogIngestWindowSize = 60;
    private const int MinLogIngestSamples = 10;
    private readonly Queue<double> _logIngestSamples = new(LogIngestWindowSize);

    /// <summary>
    /// EMA-smoothed RCON round-trip time in milliseconds (two-way).
    /// Returns null until at least <see cref="MinRconSamples"/> samples are collected.
    /// </summary>
    public double? RconRoundTripMs
    {
        get
        {
            lock (_lock)
            {
                return _rconSampleCount >= MinRconSamples ? _rconRtt : null;
            }
        }
    }

    /// <summary>
    /// Sliding-window median of log ingest latency in milliseconds — the one-way path
    /// a natural game-side log event traverses: game writes log line → GLS file poll →
    /// GLS forward → IW4MAdmin parse. Does NOT include RCon out-leg (use
    /// <see cref="RconRoundTripMs"/>/2 to compose a probe round-trip if needed) nor
    /// C#-side semaphore/flood-protect/retry.
    /// Computed per-sample by subtracting estimated one-way RCon delivery (rtt/2) from
    /// the measured probe-to-parse window, then aggregated as a median over the most
    /// recent <see cref="LogIngestWindowSize"/> samples. Median rejects per-sample
    /// outliers from probe-vs-poll-cycle phase aliasing.
    /// Requires GSC companion + established RCon RTT. Returns null until at least
    /// <see cref="MinLogIngestSamples"/> samples are in the window.
    /// </summary>
    public double? GameLogIngestMs
    {
        get
        {
            lock (_lock)
            {
                if (_logIngestSamples.Count < MinLogIngestSamples)
                {
                    return null;
                }

                var sorted = _logIngestSamples.OrderBy(v => v).ToArray();
                var mid = sorted.Length / 2;
                return sorted.Length % 2 == 1
                    ? sorted[mid]
                    : (sorted[mid - 1] + sorted[mid]) / 2.0;
            }
        }
    }

    /// <summary>
    /// Timestamp of the most recent RCON RTT sample.
    /// </summary>
    public DateTime? LastRconSample { get; private set; }

    /// <summary>
    /// Timestamp of the most recent log probe sample.
    /// </summary>
    public DateTime? LastLogProbeSample { get; private set; }

    public void RecordRconRtt(double rttMs)
    {
        if (rttMs < 0)
        {
            return;
        }

        lock (_lock)
        {
            if (_rconSampleCount == 0)
            {
                _rconRtt = rttMs;
            }
            else
            {
                _rconRtt = _alpha * rttMs + (1 - _alpha) * _rconRtt;
            }

            _rconSampleCount++;
            LastRconSample = DateTime.UtcNow;
        }
    }

    public void RecordLogProbeLatency(double pipelineMs)
    {
        if (pipelineMs < 0)
        {
            return;
        }

        lock (_lock)
        {
            _logIngestSamples.Enqueue(pipelineMs);
            while (_logIngestSamples.Count > LogIngestWindowSize)
            {
                _logIngestSamples.Dequeue();
            }
            LastLogProbeSample = DateTime.UtcNow;
        }
    }
}
