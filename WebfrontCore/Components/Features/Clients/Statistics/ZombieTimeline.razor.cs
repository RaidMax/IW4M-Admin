using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieTimeline
{
    [Parameter, EditorRequired]
    public List<ZombieMatchHistoryEvent> Events { get; set; } = [];

    /// <summary>
    /// Optional ranges where the player wasn't tracked in the match. Each tuple is
    /// (startSeconds, endSeconds, tooltip) — start/end are elapsed-since-match-start,
    /// tooltip is the description shown on hover (caller chooses based on whether the
    /// gap is at the start, end, or middle of the player's round set so we can describe
    /// joined-late and left-early specifically without speculating on internal gaps).
    /// Each range renders as a dashed-red vertical bar so users see a deliberate
    /// break-in-continuity rather than an unexplained empty stretch on the axis.
    /// </summary>
    [Parameter] public List<ZombieTimelineGaps.GapRange> GapRanges { get; set; } = [];

    [Parameter] public double ZoomLevel { get; set; } = 1;
    [Parameter] public string Filter { get; set; } = "all";
    [Parameter] public EventCallback<double> ZoomLevelChanged { get; set; }
    [Parameter] public EventCallback<string> FilterChanged { get; set; }

    private Task ChangeZoom(double delta) =>
        ZoomLevelChanged.InvokeAsync(Math.Clamp(ZoomLevel + delta, 1, 5));

    private Task SelectFilter(string filter) => FilterChanged.InvokeAsync(filter);

    private Task OnWheel(WheelEventArgs e) => ChangeZoom(e.DeltaY < 0 ? 0.25 : -0.25);

    private static string FormatTooltipText(ZombieMatchHistoryEvent evt) =>
        $"{evt.Time} • {evt.Label}";

    private static (string Bg, string Text, string Icon, int ZIndex, bool IsTick) GetEventVisuals(string category) =>
        category switch
        {
            "powerup" => ("bg-yellow-400", "text-yellow-400", "ph-lightning", 30, false),
            "danger" => ("bg-orange-500", "text-orange-500", "ph-warning", 40, false),
            "critical" => ("bg-error", "text-error", "ph-skull", 50, false),
            "success" => ("bg-success", "text-success", "ph-heartbeat", 30, false),
            "perk" => ("bg-purple-500", "text-purple-500", "ph-pill", 25, false),
            "weapon" => ("bg-info", "text-info", "ph-knife", 20, false),
            // Abandon = walked away from PaP, lost 5000pts. Mirrors box-pass
            // (orange = wasted spend) but keeps the weapon-family icon so it
            // still reads as a PaP event in the timeline.
            "weapon-abandon" => ("bg-orange-400", "text-orange-400", "ph-knife", 20, false),
            "box" => ("bg-blue-400", "text-blue-400", "ph-cube", 20, false),
            "box-pass" => ("bg-orange-400", "text-orange-400", "ph-cube", 20, false),
            // Teddy bear: distinct from take/pass — engine refunds the cost
            // and moves the box. Pink to read as "rare/whimsical" rather than
            // "missed weapon" (orange) or "got weapon" (blue).
            "box-teddy" => ("bg-pink-400", "text-pink-400", "ph-spiral", 25, false),
            "door" => ("bg-amber-500", "text-amber-500", "ph-door-open", 15, false),
            "trap" => ("bg-red-400", "text-red-400", "ph-lightning", 25, false),
            "build" => ("bg-emerald-500", "text-emerald-500", "ph-wrench", 20, false),
            "session-join" => ("bg-slate-400", "text-slate-400", "ph-sign-in", 15, false),
            "session-leave" => ("bg-slate-500", "text-slate-500", "ph-sign-out", 15, false),
            _ => ("bg-muted", "text-muted", "", 10, true)
        };

    // Round markers are positional context — always visible regardless of filter so the
    // user doesn't lose their place when narrowing to Danger/Drops/Economy.
    private static bool EventMatchesFilter(ZombieMatchHistoryEvent evt, string filter)
    {
        if (evt.Category == "round") return true;
        return filter switch
        {
            "critical" => evt.Category is "danger" or "critical",
            // "Drops" = things that fall from zombies (nukes, max ammo, insta-kill, etc.)
            "powerups" => evt.Category is "powerup",
            // Perks are bought with points, so they belong to Economy alongside weapons/doors/etc.
            "economy" => evt.Category is "weapon" or "weapon-abandon" or "box" or "box-pass" or "door" or "trap" or "build" or "perk",
            _ => true
        };
    }
}
