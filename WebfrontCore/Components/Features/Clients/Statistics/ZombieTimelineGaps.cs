using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

/// <summary>
/// Helper for deriving timeline gap ranges (start/end seconds, elapsed-since-match-start)
/// from a player's round set + the match's round-marker events. Lets both
/// <see cref="ZombieMatchHistory"/> (per-client) and <see cref="ZombieMatchDetail"/>
/// (per-match-per-player) compute gap ranges identically before handing them to
/// <see cref="ZombieMatchScrubber"/> for rendering.
///
/// A gap is any contiguous span of rounds the player wasn't tracked in. The endpoints
/// of each gap are looked up in the round-marker events: gap (R_a..R_b) maps to
/// [seconds(R_a), seconds(R_{b+1})]. Gaps at the start (before player's first round)
/// and end (after player's last round) of the match are also surfaced.
/// </summary>
public static class ZombieTimelineGaps
{
    /// <summary>
    /// A gap range. <see cref="Compact"/> = true for mid-match interruptions that
    /// should render as a slim centered marker (point-in-time event) rather than a
    /// full-width band (which is reserved for joined-late / left-early — those
    /// describe genuine extents of player presence).
    /// </summary>
    public record GapRange(double Start, double End, string Tooltip, bool Compact);

    public static List<GapRange> Compute(
        List<ZombieMatchHistoryRound> rounds,
        List<ZombieMatchHistoryEvent> events,
        int? matchHighestRound)
    {
        if (rounds.Count == 0) return [];

        // Map roundNumber -> seconds, from the match's round-marker events.
        var roundSeconds = events
            .Where(e => e.Category == "round" && e.RoundNumber.HasValue)
            .GroupBy(e => e.RoundNumber!.Value)
            .ToDictionary(g => g.Key, g => g.Min(e => e.Seconds));

        var orderedRounds = rounds.OrderBy(r => r.RoundNumber).Select(r => r.RoundNumber).ToList();
        var firstPlayed = orderedRounds[0];
        var lastPlayed = orderedRounds[^1];

        var gaps = new List<GapRange>();

        // "Joined late" gap — from match start (round 1) up to the player's first round.
        // Only render when we know where round 1 was AND where the player's first round was.
        if (firstPlayed > 1 && roundSeconds.TryGetValue(firstPlayed, out var firstPlayedSeconds))
        {
            gaps.Add(new GapRange(0, firstPlayedSeconds, "Player joined the match late", false));
        }

        // Internal gaps between consecutive played rounds.
        for (var i = 0; i < orderedRounds.Count - 1; i++)
        {
            var current = orderedRounds[i];
            var next = orderedRounds[i + 1];
            if (next <= current + 1) continue;

            // Gap covers rounds (current+1)..(next-1). In seconds, from when round
            // (current+1) started up to when round next started.
            if (roundSeconds.TryGetValue(current + 1, out var gapStart) &&
                roundSeconds.TryGetValue(next, out var gapEnd))
            {
                gaps.Add(new GapRange(gapStart, gapEnd, "Player wasn't tracked here", true));
            }
        }

        // "Left early" gap — from end of player's last round to the match's last round.
        if (matchHighestRound is { } highest && lastPlayed < highest)
        {
            // Gap starts at the round after the player's last played round.
            if (roundSeconds.TryGetValue(lastPlayed + 1, out var gapStart))
            {
                // End at the last round-marker we have, or fall back to max event time.
                var lastEventSeconds = events.Count > 0 ? events.Max(e => e.Seconds) : gapStart;
                gaps.Add(new GapRange(gapStart, lastEventSeconds, "Player left or disconnected before the match ended", false));
            }
        }

        // "Round markers missing" gap — round-event continuity loss. Fires when the
        // logged RoundCompleted events skip numbers AND the player did track rounds
        // in the missing span (so we know they played, but the server-side markers
        // didn't make it). Most likely cause: IW4MAdmin restart between rounds.
        // Skipped when the player also has a gap there — the player-rounds gap above
        // already covers that case with a more accurate "wasn't tracked" tooltip.
        var playedRounds = orderedRounds.ToHashSet();
        var sortedRoundEvents = events
            .Where(e => e.Category == "round" && e.RoundNumber.HasValue)
            .OrderBy(e => e.RoundNumber!.Value)
            .Select(e => (Round: e.RoundNumber!.Value, e.Seconds))
            .ToList();

        for (var i = 0; i < sortedRoundEvents.Count - 1; i++)
        {
            var cur = sortedRoundEvents[i];
            var next = sortedRoundEvents[i + 1];
            if (next.Round <= cur.Round + 1) continue;

            var anyMissingWasPlayed = false;
            for (var r = cur.Round + 1; r < next.Round; r++)
            {
                if (!playedRounds.Contains(r)) continue;
                anyMissingWasPlayed = true;
                break;
            }
            if (!anyMissingWasPlayed) continue;

            gaps.Add(new GapRange(cur.Seconds, next.Seconds, "Match data missing here", true));
        }

        return gaps;
    }
}
