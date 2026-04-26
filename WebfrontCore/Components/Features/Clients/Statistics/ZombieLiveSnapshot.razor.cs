using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieLiveSnapshot
{
    [Parameter, EditorRequired]
    public ZombieLiveMatchSnapshot Snapshot { get; set; } = default!;

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
            _ => ("bg-muted", "text-muted", "", true)
        };
}
