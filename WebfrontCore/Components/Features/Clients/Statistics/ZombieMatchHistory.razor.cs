using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieMatchHistory
{
    [Parameter, EditorRequired]
    public List<ZombieMatchHistoryMatch> Matches { get; set; } = [];

    private readonly Dictionary<int, MatchCardState> _cardStates = new();

    private MatchCardState GetCardState(int matchId)
    {
        if (!_cardStates.TryGetValue(matchId, out var state))
        {
            state = new MatchCardState();
            _cardStates[matchId] = state;
        }

        return state;
    }

    private void ToggleExpand(int matchId) =>
        GetCardState(matchId).IsExpanded = !GetCardState(matchId).IsExpanded;

    private void SetZoom(int matchId, double delta) =>
        GetCardState(matchId).ZoomLevel = Math.Clamp(GetCardState(matchId).ZoomLevel + delta, 1, 5);

    private void OnTimelineWheel(WheelEventArgs e, int matchId)
    {
        var delta = e.DeltaY < 0 ? 0.25 : -0.25;
        SetZoom(matchId, delta);
    }

    private void SetFilter(int matchId, string filter) =>
        GetCardState(matchId).TimelineFilter = filter;

    private static string FormatTooltipText(ZombieMatchHistoryEvent evt)
    {
        var visuals = GetEventVisuals(evt.Category);
        return $"{evt.Time} — {evt.Label}";
    }

    private static (string Bg, string Text, string Icon, int ZIndex, bool IsTick) GetEventVisuals(string category) =>
        category switch
        {
            "powerup" => ("bg-yellow-400", "text-yellow-400", "ph-lightning", 30, false),
            "danger" => ("bg-orange-500", "text-orange-500", "ph-warning", 40, false),
            "critical" => ("bg-error", "text-error", "ph-skull", 50, false),
            "success" => ("bg-success", "text-success", "ph-heartbeat", 30, false),
            "info" => ("bg-info", "text-info", "ph-pill", 20, false),
            _ => ("bg-muted", "text-muted", "", 10, true)
        };

    private static bool EventMatchesFilter(ZombieMatchHistoryEvent evt, string filter) => filter switch
    {
        "critical" => evt.Category is "danger" or "critical",
        "powerups" => evt.Category == "powerup",
        _ => true
    };

    private class MatchCardState
    {
        public bool IsExpanded { get; set; }
        public double ZoomLevel { get; set; } = 1;
        public string TimelineFilter { get; set; } = "all";
    }
}
