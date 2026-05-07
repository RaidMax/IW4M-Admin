using System;

namespace SharedLibraryCore.Helpers;

public class ServerLatencyMetrics(double alpha = 0.3)
{
    private readonly Lock _lock = new();
    private readonly double _alpha = Math.Clamp(alpha, 0.01, 1.0);
    private int _rconSampleCount;
    private int _logProbeSampleCount;
    private double _rconRtt;
    private double _logIngest;
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
    /// EMA-smoothed log ingest latency in milliseconds — the one-way path a natural
    /// game-side log event traverses: game writes log line → GLS file poll → GLS forward →
    /// IW4MAdmin parse. Does NOT include RCon out-leg (use <see cref="RconRoundTripMs"/>/2
    /// to compose a probe round-trip if needed) nor C#-side semaphore/flood-protect/retry.
    /// Computed by subtracting estimated one-way RCon delivery (rtt/2) from the measured
    /// probe-to-parse window, so the value reflects what a chat or kill event would experience.
    /// Requires GSC companion + established RCon RTT. Returns null until both have sufficient samples.
    /// </summary>
    public double? GameLogIngestMs
    {
        get
        {
            lock (_lock)
            {
                return _logProbeSampleCount >= MinSamplesRequired ? _logIngest : null;
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
                _logIngest = totalMs;
            }
            else
            {
                _logIngest = _alpha * totalMs + (1 - _alpha) * _logIngest;
            }

            _logProbeSampleCount++;
            LastLogProbeSample = DateTime.UtcNow;
        }
    }
}
