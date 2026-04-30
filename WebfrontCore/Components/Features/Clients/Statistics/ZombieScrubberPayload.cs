using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

/// <summary>
/// Slim DTO sent to <c>zombie-scrubber.js</c> via JSInterop. Carries per-lane events and
/// shared time axis. Built from either a full <see cref="SharedLibraryCore.Interfaces.ZombieMatchDetail"/>
/// (multi-player match-overview) or a single-player <see cref="ZombieMatchHistoryMatch"/>
/// (per-client view), since the JS renderer treats them identically — single-lane render
/// is auto-detected from <c>Lanes.Count == 1</c>.
/// </summary>
public sealed class ZombieScrubberPayload
{
    public double MinSeconds { get; set; }
    public double MaxSeconds { get; set; }
    public int HighestRound { get; set; }
    public List<ScrubberRoundBand> RoundBands { get; set; } = [];
    public List<ScrubberLane> Lanes { get; set; } = [];

    public static ZombieScrubberPayload From(SharedLibraryCore.Interfaces.ZombieMatchDetail detail)
    {
        var eeEvent = BuildEasterEggMarker(detail.EasterEggOccurredAt, detail.EasterEggRound, detail.Date);

        var lanes = detail.Players
            .Select(p =>
            {
                var events = p.Events.Select(ScrubberEvent.From).ToList();
                if (eeEvent is not null) events.Add(eeEvent);
                events = events.OrderBy(e => e.Seconds).ToList();

                return new ScrubberLane
                {
                    ClientId = p.ClientId,
                    Name = p.Name,
                    IsQualified = p.IsQualified,
                    Events = events,
                    Gaps = ZombieTimelineGaps.Compute(p.Rounds, p.Events, detail.HighestRound)
                        .Select(g => new ScrubberGap
                        {
                            Start = g.Start,
                            End = g.End,
                            Tooltip = g.Tooltip,
                            Compact = g.Compact
                        })
                        .ToList()
                };
            })
            .ToList();

        return Build(lanes, detail.HighestRound, detail.Players.SelectMany(p => p.Events));
    }

    public static ZombieScrubberPayload From(ZombieMatchHistoryMatch match, string playerName, int clientId)
    {
        // Per-client view doesn't carry MatchStartDate — synthesize from Date string
        // when an EE marker is wanted. Round bands derive from match.Events so they
        // share the same elapsed-seconds origin; EasterEggOccurredAt is wall-clock,
        // so we map it onto the same axis using the parsed start date.
        DateTimeOffset? matchStart = DateTimeOffset.TryParse(match.Date, out var parsed) ? parsed : null;
        var eeEvent = matchStart is not null
            ? BuildEasterEggMarker(match.EasterEggOccurredAt, match.EasterEggRound, matchStart.Value)
            : null;

        var laneEvents = match.Events.Select(ScrubberEvent.From).ToList();
        if (eeEvent is not null) laneEvents.Add(eeEvent);
        laneEvents = laneEvents.OrderBy(e => e.Seconds).ToList();

        var lane = new ScrubberLane
        {
            ClientId = clientId,
            Name = playerName,
            // Per-client view: the row only renders when the player qualified, so the
            // lane is always qualified by construction.
            IsQualified = true,
            Events = laneEvents,
            Gaps = ZombieTimelineGaps.Compute(match.Rounds, match.Events, match.HighestRound)
                .Select(g => new ScrubberGap
                {
                    Start = g.Start,
                    End = g.End,
                    Tooltip = g.Tooltip,
                    Compact = g.Compact
                })
                .ToList()
        };

        return Build([lane], match.HighestRound, match.Events);
    }

    /// <summary>
    /// Synthesizes an EE-completed marker event at the recorded fire time, on the
    /// same elapsed-seconds axis the rest of the timeline uses. Returns null when
    /// EasterEggOccurredAt is absent — null fields collapse to "no marker" in the UI.
    /// </summary>
    private static ScrubberEvent? BuildEasterEggMarker(
        DateTimeOffset? occurredAt, int? round, DateTimeOffset matchStart)
    {
        if (occurredAt is null) return null;
        var elapsed = Math.Max(0, (occurredAt.Value - matchStart).TotalSeconds);
        return new ScrubberEvent
        {
            Seconds = elapsed,
            Time = FormatElapsed(elapsed),
            Label = round is { } r ? $"Easter Egg Complete (R{r})" : "Easter Egg Complete",
            Category = "easter-egg",
            RoundNumber = round
        };
    }

    private static string FormatElapsed(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static ZombieScrubberPayload Build(
        List<ScrubberLane> lanes,
        int highestRound,
        IEnumerable<ZombieMatchHistoryEvent> allEventsForBands)
    {
        // Round bands derive from any player's round-marker events. Group by round, take
        // the earliest second-mark per round (covers the case where a late joiner has a
        // later "round X" marker — the first-to-fire is the actual transition).
        var roundSeconds = allEventsForBands
            .Where(e => e.Category == "round" && e.RoundNumber.HasValue)
            .GroupBy(e => e.RoundNumber!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new { Round = g.Key, Seconds = g.Min(e => e.Seconds) })
            .ToList();

        var bands = new List<ScrubberRoundBand>();
        for (var i = 0; i < roundSeconds.Count; i++)
        {
            var cur = roundSeconds[i];
            // End of band = start of next round, or extended to last event time on the
            // final round so the band visually covers the tail of the match.
            var end = i + 1 < roundSeconds.Count
                ? roundSeconds[i + 1].Seconds
                : Math.Max(cur.Seconds, AllEventsMaxSeconds(lanes));

            bands.Add(new ScrubberRoundBand
            {
                RoundNumber = cur.Round,
                StartSeconds = cur.Seconds,
                EndSeconds = end
            });
        }

        var allTimes = lanes.SelectMany(l => l.Events).Select(e => e.Seconds)
            .Concat(lanes.SelectMany(l => l.Gaps).SelectMany(g => new[] { g.Start, g.End }))
            .Concat(bands.SelectMany(b => new[] { b.StartSeconds, b.EndSeconds }))
            .ToList();

        return new ZombieScrubberPayload
        {
            MinSeconds = allTimes.Count > 0 ? allTimes.Min() : 0,
            MaxSeconds = allTimes.Count > 0 ? allTimes.Max() : 1,
            HighestRound = highestRound,
            RoundBands = bands,
            Lanes = lanes
        };
    }

    private static double AllEventsMaxSeconds(List<ScrubberLane> lanes)
    {
        var allEventTimes = lanes.SelectMany(l => l.Events).Select(e => e.Seconds).ToList();
        return allEventTimes.Count > 0 ? allEventTimes.Max() : 0;
    }
}

public sealed class ScrubberRoundBand
{
    public int RoundNumber { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
}

public sealed class ScrubberLane
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsQualified { get; set; }
    public List<ScrubberEvent> Events { get; set; } = [];
    public List<ScrubberGap> Gaps { get; set; } = [];
}

public sealed class ScrubberEvent
{
    public double Seconds { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int? RoundNumber { get; set; }

    public static ScrubberEvent From(ZombieMatchHistoryEvent e) => new()
    {
        Seconds = e.Seconds,
        Time = e.Time,
        Label = e.Label,
        Category = e.Category,
        RoundNumber = e.RoundNumber
    };
}

public sealed class ScrubberGap
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Tooltip { get; set; } = string.Empty;
    public bool Compact { get; set; }
}
