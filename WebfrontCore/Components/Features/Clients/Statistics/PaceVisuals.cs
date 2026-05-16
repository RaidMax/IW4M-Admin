using Data.Models.Zombie;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

/// <summary>
/// Shared rendering helpers for round-pace tinting. Used by both the post-match
/// round table and the live snapshot so a "12% slower" round looks identical
/// across both surfaces.
/// </summary>
public static class PaceVisuals
{
    /// <summary>
    /// Tailwind text-colour class for the 5-band pace scale. Symmetric around
    /// neutral; vivid shades for the &gt;15% extremes, pale shades for the inner
    /// 5–15% bands. Pale-not-dark on dark theme — opacity-dimmed greens/reds
    /// blend into the background instead of reading as "lighter than vivid".
    /// </summary>
    public static string ColorClass(PaceBand? band) => band switch
    {
        PaceBand.MuchFaster => "text-emerald-400",
        PaceBand.Faster     => "text-emerald-200",
        PaceBand.Slower     => "text-rose-200",
        PaceBand.MuchSlower => "text-rose-400",
        _                   => "text-subtle",
    };

    /// <summary>
    /// Classify a live elapsed duration against an EMA target using the same
    /// thresholds as <c>ZombieRoundEmaService.Classify</c>. Returns null when
    /// the EMA target is not yet known. Lets the live banner shift the
    /// elapsed text colour every second the round runs without round-tripping
    /// to the server.
    /// </summary>
    public static (PaceBand Band, double Ratio)? ClassifyLive(double elapsedSeconds, double? emaSeconds)
    {
        if (emaSeconds is not { } typical || typical <= 0 || elapsedSeconds <= 0) return null;
        var ratio = (elapsedSeconds - typical) / typical;
        var band = ratio switch
        {
            < -0.15 => PaceBand.MuchFaster,
            < -0.05 => PaceBand.Faster,
            <= 0.05 => PaceBand.Neutral,
            <= 0.15 => PaceBand.Slower,
            _       => PaceBand.MuchSlower,
        };
        return (band, ratio);
    }

    /// <summary>
    /// Format mm:ss / h:mm:ss for a duration in seconds. Mirrors the display
    /// format used by the post-match round table.
    /// </summary>
    public static string FormatRoundTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    /// <summary>
    /// Player-count label suitable for the pace tooltip ("solo" / "2P" etc).
    /// Reuses the same translation keys broadcast messages use.
    /// </summary>
    public static string PlayerLabel(int? playerCount, AppState appState) => playerCount switch
    {
        1 => appState.Loc("PLUGINS_ZOMBIE_STATS_LABEL_SOLO"),
        { } n and > 1 => appState.Loc("PLUGINS_ZOMBIE_STATS_LABEL_PLAYER_SHORT").FormatExt(n),
        _ => string.Empty,
    };

    /// <summary>
    /// Tooltip text for a completed round's pace. Returns null when the round
    /// has no EMA data — caller renders the row without a tooltip.
    /// </summary>
    public static string? RoundTooltip(ZombieMatchHistoryRound round, AppState appState)
    {
        if (round.PaceBand is not { } band || round.PaceRatio is not { } ratio
            || round.PaceTypicalSeconds is not { } typical)
        {
            return null;
        }

        var playerLabel = PlayerLabel(round.PlayerCountAtRoundStart, appState);
        var typicalFormatted = FormatRoundTime(typical);
        var absPercent = Math.Abs(ratio) * 100;

        return band switch
        {
            PaceBand.Neutral => appState.Loc("WEBFRONT_ZOMBIE_ROUND_PACE_TOOLTIP_NEUTRAL")
                .FormatExt(round.RoundNumber, playerLabel, typicalFormatted),
            PaceBand.Faster or PaceBand.MuchFaster => appState.Loc("WEBFRONT_ZOMBIE_ROUND_PACE_TOOLTIP_FASTER")
                .FormatExt(absPercent.ToString("F0"), round.RoundNumber, playerLabel, typicalFormatted),
            _ => appState.Loc("WEBFRONT_ZOMBIE_ROUND_PACE_TOOLTIP_SLOWER")
                .FormatExt(absPercent.ToString("F0"), round.RoundNumber, playerLabel, typicalFormatted),
        };
    }

    /// <summary>
    /// Visuals + localized labels for the special-round chip rendered next to the
    /// round number on both the post-match Round Breakdown and the live banner.
    /// Returns null on normal rounds. Tooltip explains why pace tinting / SPH are
    /// suppressed for the round.
    /// </summary>
    public static SpecialRoundBadge? SpecialBadge(ZombieSpecialRoundType? specialType, AppState appState)
    {
        if (specialType is not { } st) return null;

        var (label, icon, colorClass) = st switch
        {
            ZombieSpecialRoundType.Dog    => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_DOG"),    "ph-dog",            "bg-amber-500/15 text-amber-300 border border-amber-500/40"),
            ZombieSpecialRoundType.Monkey => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_MONKEY"), "ph-paw-print",      "bg-rose-500/15 text-rose-300 border border-rose-500/40"),
            ZombieSpecialRoundType.Leaper => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_LEAPER"), "ph-arrow-up",       "bg-emerald-500/15 text-emerald-300 border border-emerald-500/40"),
            ZombieSpecialRoundType.Thief  => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_THIEF"),  "ph-mask-sad",       "bg-cyan-500/15 text-cyan-300 border border-cyan-500/40"),
            ZombieSpecialRoundType.Wasp   => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_WASP"),   "ph-bug",            "bg-purple-500/15 text-purple-300 border border-purple-500/40"),
            ZombieSpecialRoundType.Spider => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_SPIDER"), "ph-spiral",         "bg-lime-500/15 text-lime-300 border border-lime-500/40"),
            ZombieSpecialRoundType.Robot  => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_ROBOT"),  "ph-robot",          "bg-zinc-500/15 text-zinc-300 border border-zinc-500/40"),
            ZombieSpecialRoundType.Quad   => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_QUAD"),   "ph-squares-four",   "bg-orange-500/15 text-orange-300 border border-orange-500/40"),
            ZombieSpecialRoundType.Boss   => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_BOSS"),   "ph-skull",          "bg-red-500/15 text-red-300 border border-red-500/40"),
            ZombieSpecialRoundType.Ee     => (appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_EE"),     "ph-star-four",      "bg-fuchsia-500/15 text-fuchsia-300 border border-fuchsia-500/40"),
            _                              => (st.ToString(),                                       "ph-question",       "bg-surface-alt text-subtle border border-line"),
        };

        var tooltip = label + " — " + appState.Loc("WEBFRONT_ZOMBIE_ROUND_SPECIAL_TOOLTIP");
        return new SpecialRoundBadge(label, icon, colorClass, tooltip);
    }
}

/// <summary>
/// Render-side payload for a special-round chip — label, icon class, color class,
/// pre-built tooltip. <see cref="PaceVisuals.SpecialBadge"/> returns null on
/// normal rounds; callers gate rendering on the optional being set.
/// </summary>
public sealed record SpecialRoundBadge(string Label, string Icon, string ColorClass, string Tooltip);
