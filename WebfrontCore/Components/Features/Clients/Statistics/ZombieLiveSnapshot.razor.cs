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
            _ => ("bg-muted", "text-muted", "", true)
        };
}
