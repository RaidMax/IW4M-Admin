using System;

namespace SharedLibraryCore.Helpers;

public class ServerLatencyMetrics(double alpha = 0.3)
{
    private readonly Lock _lock = new();
    private readonly double _alpha = Math.Clamp(alpha, 0.01, 1.0);
    private int _rconSampleCount;
    private int _logProbeSampleCount;
    private double _rconRtt;
    private double _logPipeline;
    private const int MinSamplesRequired = 3;

    /// <summary>
    /// EMA-smoothed RCON round-trip time in milliseconds (two-way).
    /// Returns null until at least <see cref="MinSamplesRequired"/> samples are collected.
    /// </summary>
    public double? RconRoundTripMs
    {
        get
        {
            lock (_lock)
            {
                return _rconSampleCount >= MinSamplesRequired ? _rconRtt : null;
            }
        }
    }

    /// <summary>
    /// EMA-smoothed total log pipeline latency in milliseconds (one-way: dvar set → log line parsed).
    /// Requires GSC companion. Returns null if not available or insufficient samples.
    /// </summary>
    public double? GameLogPipelineMs
    {
        get
        {
            lock (_lock)
            {
                return _logProbeSampleCount >= MinSamplesRequired ? _logPipeline : null;
            }
        }
    }

    /// <summary>
    /// Estimated log-only overhead in milliseconds (GameLogPipeline minus estimated one-way RCON delivery).
    /// Returns null if either component is unavailable.
    /// </summary>
    public double? EstimatedLogOverheadMs
    {
        get
        {
            var rtt = RconRoundTripMs;
            var log = GameLogPipelineMs;
            if (rtt is null || log is null)
            {
                return null;
            }

            return Math.Max(0, log.Value - rtt.Value / 2.0);
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

    public void RecordLogProbeLatency(double totalMs)
    {
        if (totalMs < 0)
        {
            return;
        }

        lock (_lock)
        {
            if (_logProbeSampleCount == 0)
            {
                _logPipeline = totalMs;
            }
            else
            {
                _logPipeline = _alpha * totalMs + (1 - _alpha) * _logPipeline;
            }

            _logProbeSampleCount++;
            LastLogProbeSample = DateTime.UtcNow;
        }
    }
}
