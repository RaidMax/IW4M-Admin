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

    /// <summary>
    /// Match-level events (no per-player attribution) — EE step markers, canonical
    /// EE-complete marker. JS renders these in a tick band above the lanes rather
    /// than duplicating them onto every lane. Empty on the per-client view.
    /// </summary>
    public List<ScrubberEvent> MatchLevelEvents { get; set; } = [];

    public static ZombieScrubberPayload From(SharedLibraryCore.Interfaces.ZombieMatchDetail detail,
        Func<string, string>? loc = null)
    {
        loc ??= s => s;

        // Match-level events (per-quest completions + step-progress dots) live in
        // their own band rather than getting cloned onto every lane — keeps the
        // visual layout clean and gives the team-milestone semantics a dedicated
        // visual home.
        var matchLevelEvents = new List<ScrubberEvent>();

        // One marker per completed quest at its own CompletedAt/Round (not the
        // match-level OccurredAt — that's the FIRST-quest-completion time, so
        // a 2-quest map would render both markers stacked at the first time).
        foreach (var quest in detail.EasterEggQuests.Where(q => q.IsComplete && q.CompletedAt.HasValue))
        {
            var elapsed = Math.Max(0, (quest.CompletedAt!.Value - detail.Date).TotalSeconds);
            var shortLabel = !string.IsNullOrEmpty(quest.ShortLocKey) ? loc(quest.ShortLocKey) : loc(quest.LocKey);
            matchLevelEvents.Add(new ScrubberEvent
            {
                Seconds = elapsed,
                Time = FormatElapsed(elapsed),
                Label = quest.CompletedRound is { } cr && cr > 0 ? $"{shortLabel} (R{cr})" : shortLabel,
                Category = "easter-egg",
                RoundNumber = quest.CompletedRound
            });
        }

        // Step records: each becomes a tick on the band, in fire order. Label uses
        // the inventory's loc-key (resolved to localized text via the supplied
        // resolver) so the tooltip reads as human-friendly step names rather than
        // raw resource keys. Step keys are globally unique, so flattening across
        // quests doesn't collide.
        var inventoryLabels = detail.EasterEggQuests
            .SelectMany(q => q.Inventory)
            .GroupBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().LocKey, StringComparer.OrdinalIgnoreCase);
        foreach (var step in detail.EasterEggQuests.SelectMany(q => q.Steps))
        {
            var elapsed = Math.Max(0, (step.OccurredAt - detail.Date).TotalSeconds);
            var rawLabel = inventoryLabels.GetValueOrDefault(step.Key, step.Key);
            matchLevelEvents.Add(new ScrubberEvent
            {
                Seconds = elapsed,
                Time = FormatElapsed(elapsed),
                Label = loc(rawLabel),
                Category = "easter-egg-step",
                RoundNumber = step.RoundNumber
            });
        }
        matchLevelEvents = matchLevelEvents.OrderBy(e => e.Seconds).ToList();

        var lanes = detail.Players
            .Select(p =>
            {
                var events = p.Events.Select(ScrubberEvent.From).ToList();
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

        return Build(lanes, detail.HighestRound, detail.Players.SelectMany(p => p.Events), matchLevelEvents);
    }

    public static ZombieScrubberPayload From(ZombieMatchHistoryMatch match, string playerName, int clientId,
        Func<string, string>? loc = null)
    {
        loc ??= s => s;

        // Per-client view doesn't carry MatchStartDate — synthesize from Date string
        // so quest CompletedAt timestamps can be projected onto the same elapsed-seconds
        // axis as match.Events. Round bands derive from match.Events directly.
        DateTimeOffset? matchStart = DateTimeOffset.TryParse(match.Date, out var parsed) ? parsed : null;

        // Match-level events: per-quest completion markers + step-progress dots, mirroring
        // the multi-player overload. Empty when matchStart can't be parsed.
        var matchLevelEvents = new List<ScrubberEvent>();
        if (matchStart is not null)
        {
            foreach (var quest in match.EasterEggQuests.Where(q => q.IsComplete && q.CompletedAt.HasValue))
            {
                var elapsed = Math.Max(0, (quest.CompletedAt!.Value - matchStart.Value).TotalSeconds);
                var shortLabel = !string.IsNullOrEmpty(quest.ShortLocKey) ? loc(quest.ShortLocKey) : loc(quest.LocKey);
                matchLevelEvents.Add(new ScrubberEvent
                {
                    Seconds = elapsed,
                    Time = FormatElapsed(elapsed),
                    Label = quest.CompletedRound is { } cr && cr > 0 ? $"{shortLabel} (R{cr})" : shortLabel,
                    Category = "easter-egg",
                    RoundNumber = quest.CompletedRound
                });
            }

            var inventoryLabels = match.EasterEggQuests
                .SelectMany(q => q.Inventory)
                .GroupBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().LocKey, StringComparer.OrdinalIgnoreCase);
            foreach (var step in match.EasterEggQuests.SelectMany(q => q.Steps))
            {
                var elapsed = Math.Max(0, (step.OccurredAt - matchStart.Value).TotalSeconds);
                var rawLabel = inventoryLabels.GetValueOrDefault(step.Key, step.Key);
                matchLevelEvents.Add(new ScrubberEvent
                {
                    Seconds = elapsed,
                    Time = FormatElapsed(elapsed),
                    Label = loc(rawLabel),
                    Category = "easter-egg-step",
                    RoundNumber = step.RoundNumber
                });
            }
            matchLevelEvents = matchLevelEvents.OrderBy(e => e.Seconds).ToList();
        }

        var laneEvents = match.Events.Select(ScrubberEvent.From).OrderBy(e => e.Seconds).ToList();

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

        return Build([lane], match.HighestRound, match.Events, matchLevelEvents);
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
        IEnumerable<ZombieMatchHistoryEvent> allEventsForBands,
        List<ScrubberEvent> matchLevelEvents)
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
            .Concat(matchLevelEvents.Select(e => e.Seconds))
            .ToList();

        return new ZombieScrubberPayload
        {
            MinSeconds = allTimes.Count > 0 ? allTimes.Min() : 0,
            MaxSeconds = allTimes.Count > 0 ? allTimes.Max() : 1,
            HighestRound = highestRound,
            RoundBands = bands,
            Lanes = lanes,
            MatchLevelEvents = matchLevelEvents
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
