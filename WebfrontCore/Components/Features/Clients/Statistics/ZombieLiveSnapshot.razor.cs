using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieLiveSnapshot
{
    [Parameter, EditorRequired]
    public ZombieLiveMatchSnapshot Snapshot { get; set; } = default!;

    [Inject] public required AppState AppState { get; set; }

    /// <summary>
    /// Per-quest palette for the live modal's Objectives column. Returns the
    /// title/bar/count text class + chip classes for done/partial/default
    /// states. Explicit <paramref name="color"/> wins; null falls back to
    /// song=purple, main=amber (the pre-Color behaviour).
    /// </summary>
    private sealed record LivePalette(
        string TitleText,
        string BarBg,
        string CountText,
        string ChipDone,
        string ChipPartial,
        string ChipDefault);

    private static LivePalette ResolveLivePalette(string? color, bool isSong)
    {
        if (!string.IsNullOrEmpty(color))
        {
            // Literal class strings per supported colour so Tailwind's purge
            // picks them up. Done = -400/15 fill + -400/40 border + -200 text;
            // partial = -400/5 fill + -400/30 border + -300 text.
            return color switch
            {
                "purple"  => new("text-purple-300",  "bg-purple-400",  "text-purple-300",
                                 "bg-purple-400/15 border-purple-400/40 text-purple-200",
                                 "bg-purple-400/5 border-purple-400/30 text-purple-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "red"     => new("text-red-400",     "bg-red-400",     "text-red-400",
                                 "bg-red-400/15 border-red-400/40 text-red-200",
                                 "bg-red-400/5 border-red-400/30 text-red-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "indigo"  => new("text-indigo-400",  "bg-indigo-400",  "text-indigo-400",
                                 "bg-indigo-400/15 border-indigo-400/40 text-indigo-200",
                                 "bg-indigo-400/5 border-indigo-400/30 text-indigo-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "amber"   => new("text-amber-400",   "bg-amber-400",   "text-amber-400",
                                 "bg-amber-400/15 border-amber-400/40 text-amber-200",
                                 "bg-amber-400/5 border-amber-400/30 text-amber-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "cyan"    => new("text-cyan-400",    "bg-cyan-400",    "text-cyan-400",
                                 "bg-cyan-400/15 border-cyan-400/40 text-cyan-200",
                                 "bg-cyan-400/5 border-cyan-400/30 text-cyan-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "emerald" => new("text-emerald-400", "bg-emerald-400", "text-emerald-400",
                                 "bg-emerald-400/15 border-emerald-400/40 text-emerald-200",
                                 "bg-emerald-400/5 border-emerald-400/30 text-emerald-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "rose"    => new("text-rose-400",    "bg-rose-400",    "text-rose-400",
                                 "bg-rose-400/15 border-rose-400/40 text-rose-200",
                                 "bg-rose-400/5 border-rose-400/30 text-rose-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "teal"    => new("text-teal-400",    "bg-teal-400",    "text-teal-400",
                                 "bg-teal-400/15 border-teal-400/40 text-teal-200",
                                 "bg-teal-400/5 border-teal-400/30 text-teal-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "orange"  => new("text-orange-400",  "bg-orange-400",  "text-orange-400",
                                 "bg-orange-400/15 border-orange-400/40 text-orange-200",
                                 "bg-orange-400/5 border-orange-400/30 text-orange-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "fuchsia" => new("text-fuchsia-400", "bg-fuchsia-400", "text-fuchsia-400",
                                 "bg-fuchsia-400/15 border-fuchsia-400/40 text-fuchsia-200",
                                 "bg-fuchsia-400/5 border-fuchsia-400/30 text-fuchsia-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                "sky"     => new("text-sky-400",     "bg-sky-400",     "text-sky-400",
                                 "bg-sky-400/15 border-sky-400/40 text-sky-200",
                                 "bg-sky-400/5 border-sky-400/30 text-sky-300",
                                 "bg-surface/60 border-line/40 text-subtle"),
                _ => ResolveLivePalette(null, isSong),
            };
        }

        // Fallback — pre-Color behaviour preserved exactly so quests without
        // an explicit Color render identically to before.
        return isSong
            ? new LivePalette(
                "text-purple-300", "bg-purple-300", "text-purple-300",
                "bg-purple-400/15 border-purple-400/40 text-purple-200",
                "bg-purple-400/5 border-purple-400/30 text-purple-300",
                "bg-surface/60 border-line/40 text-subtle")
            : new LivePalette(
                "text-amber-400", "bg-amber-400", "text-amber-400",
                "bg-amber-500/15 border-amber-500/40 text-amber-300",
                "bg-amber-500/5 border-amber-500/30 text-amber-400",
                "bg-surface/60 border-line/40 text-subtle");
    }

    private static (string Label, string Color, string Bg, string Dot, bool Animate, string Tooltip)
        StatusVisuals(ZombieLivePlayerStatus status, AppState appState) => status switch
    {
        ZombieLivePlayerStatus.Alive => (appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_ALIVE"), "text-success", "bg-success/10", "bg-success", true,
            appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_ALIVE_TOOLTIP")),
        ZombieLivePlayerStatus.Down => (appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_DOWN"), "text-orange-400", "bg-orange-400/10", "bg-orange-400", true,
            appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_DOWN_TOOLTIP")),
        ZombieLivePlayerStatus.Dead => (appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_DEAD"), "text-error", "bg-error/10", "bg-error", false,
            appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_DEAD_TOOLTIP")),
        _ => (appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_DISCONNECTED"), "text-subtle", "bg-subtle/10", "bg-subtle", false,
            appState.Loc("WEBFRONT_ZOMBIE_LIVE_STATUS_DISCONNECTED_TOOLTIP")),
    };

    /// <summary>
    /// Tooltip text for a round-completed event in the live timeline. Reuses the
    /// same tooltip vocabulary as the round-table breakdown so users see a
    /// consistent description across both surfaces.
    /// </summary>
    private string? LiveTimelinePaceTooltip(ZombieMatchHistoryEvent ev)
    {
        if (ev.PaceBand is not { } band || ev.PaceRatio is not { } ratio
            || ev.PaceTypicalSeconds is not { } typical || ev.RoundNumber is not { } round)
        {
            return null;
        }

        var playerLabel = PaceVisuals.PlayerLabel(Snapshot.CurrentRoundPlayerCount, AppState);
        var typicalFormatted = PaceVisuals.FormatRoundTime(typical);
        var absPercent = Math.Abs(ratio) * 100;

        return band switch
        {
            PaceBand.Neutral => AppState.Loc("WEBFRONT_ZOMBIE_ROUND_PACE_TOOLTIP_NEUTRAL")
                .FormatExt(round, playerLabel, typicalFormatted),
            PaceBand.Faster or PaceBand.MuchFaster => AppState.Loc("WEBFRONT_ZOMBIE_ROUND_PACE_TOOLTIP_FASTER")
                .FormatExt(absPercent.ToString("F0"), round, playerLabel, typicalFormatted),
            _ => AppState.Loc("WEBFRONT_ZOMBIE_ROUND_PACE_TOOLTIP_SLOWER")
                .FormatExt(absPercent.ToString("F0"), round, playerLabel, typicalFormatted),
        };
    }

    /// <summary>
    /// Tooltip text for the live banner's elapsed timer. When the EMA target is
    /// unknown (cold cell) we fall back to the original "round in progress" string;
    /// when known we surface the current pace delta vs typical so the viewer
    /// understands what the colour means.
    /// </summary>
    private string LiveBannerTooltip(double elapsedSeconds, (PaceBand Band, double Ratio)? band)
    {
        if (Snapshot.CurrentRoundEmaSeconds is not { } typical || typical <= 0 || band is not { } b)
        {
            return AppState.Loc("WEBFRONT_ZOMBIE_LIVE_CURRENT_ROUND_TOOLTIP");
        }

        var playerLabel = PaceVisuals.PlayerLabel(Snapshot.CurrentRoundPlayerCount, AppState);
        var typicalFormatted = PaceVisuals.FormatRoundTime(typical);
        var absPercent = Math.Abs(b.Ratio) * 100;

        return b.Band switch
        {
            PaceBand.Neutral => AppState.Loc("WEBFRONT_ZOMBIE_LIVE_PACE_TOOLTIP_NEUTRAL")
                .FormatExt(Snapshot.CurrentRound, playerLabel, typicalFormatted),
            PaceBand.Faster or PaceBand.MuchFaster => AppState.Loc("WEBFRONT_ZOMBIE_LIVE_PACE_TOOLTIP_FASTER")
                .FormatExt(absPercent.ToString("F0"), Snapshot.CurrentRound, playerLabel, typicalFormatted),
            _ => AppState.Loc("WEBFRONT_ZOMBIE_LIVE_PACE_TOOLTIP_SLOWER")
                .FormatExt(absPercent.ToString("F0"), Snapshot.CurrentRound, playerLabel, typicalFormatted),
        };
    }

    // Mirrors ZombieTimeline.GetEventVisuals — same icons/colors so the live activity
    // feed matches the post-match timeline. Kept inline (rather than reusing the
    // ZombieTimeline helper) because this view renders events as a list, not a strip,
    // and doesn't need the ZIndex / IsTick bar-vs-icon decision logic.
    private static (string Bg, string Text, string Icon, bool IsTick) GetEventVisuals(string category) =>
        category switch
        {
            "powerup" => ("bg-yellow-400", "text-yellow-400", "ph-lightning", false),
            "danger" => ("bg-orange-500", "text-orange-500", "ph-warning", false),
            "critical" => ("bg-error", "text-error", "ph-skull", false),
            "success" => ("bg-success", "text-success", "ph-heartbeat", false),
            "perk" => ("bg-purple-500", "text-purple-500", "ph-pill", false),
            "weapon" => ("bg-info", "text-info", "ph-knife", false),
            "box" => ("bg-blue-400", "text-blue-400", "ph-cube", false),
            "box-pass" => ("bg-orange-400", "text-orange-400", "ph-cube", false),
            "box-teddy" => ("bg-pink-400", "text-pink-400", "ph-spiral", false),
            "door" => ("bg-amber-500", "text-amber-500", "ph-door-open", false),
            "trap" => ("bg-red-400", "text-red-400", "ph-lightning", false),
            "build" => ("bg-emerald-500", "text-emerald-500", "ph-wrench", false),
            "session-join" => ("bg-slate-400", "text-slate-400", "ph-sign-in", false),
            "session-leave" => ("bg-slate-500", "text-slate-500", "ph-sign-out", false),
            // Mirrors zombie-scrubber.js easter-egg-step (amber + ph-trophy) so the
            // live feed matches the post-match scrubber's EE dot visuals.
            "easter-egg-step" => ("bg-amber-500", "text-amber-500", "ph-trophy", false),
            "easter-egg" => ("bg-yellow-400", "text-yellow-400", "ph-trophy", false),
            "power-on" => ("bg-yellow-400", "text-yellow-400", "ph-lightning", false),
            "power-off" => ("bg-slate-400", "text-slate-400", "ph-lightning-slash", false),
            "weapon-abandon" => ("bg-orange-400", "text-orange-400", "ph-knife", false),
            "bank-deposit" => ("bg-green-500", "text-green-500", "ph-piggy-bank", false),
            "bank-withdraw" => ("bg-amber-400", "text-amber-400", "ph-hand-coins", false),
            "locker-store" => ("bg-blue-400", "text-blue-400", "ph-lockers", false),
            "locker-retrieve" => ("bg-emerald-400", "text-emerald-400", "ph-lockers", false),
            "gum-activate" => ("bg-pink-500", "text-pink-500", "ph-sparkle", false),
            "gum-take" => ("bg-purple-500", "text-purple-500", "ph-gift", false),
            "gum-leave" => ("bg-slate-400", "text-slate-400", "ph-heart-break", false),
            _ => ("bg-muted", "text-muted", "", true)
        };
}
