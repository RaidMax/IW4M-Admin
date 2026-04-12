using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieMatchDetail
{
    [Parameter, EditorRequired]
    public SharedLibraryCore.Interfaces.ZombieMatchDetail Detail { get; set; } = default!;

    /// <summary>
    /// When set, the player tab bar is hidden — selection is controlled externally.
    /// </summary>
    [Parameter]
    public int? SelectedClientId { get; set; }

    private int _internalSelectedClientId;
    private double _zoomLevel = 1;
    private string _timelineFilter = "all";

    private int ActiveClientId => SelectedClientId ?? _internalSelectedClientId;

    protected override void OnParametersSet()
    {
        if (Detail.Players.Count > 0 && Detail.Players.All(p => p.ClientId != ActiveClientId))
        {
            _internalSelectedClientId = Detail.Players[0].ClientId;
        }
    }

    private ZombieMatchDetailPlayer? SelectedPlayer =>
        Detail.Players.FirstOrDefault(p => p.ClientId == ActiveClientId);

    private void SelectPlayer(int clientId) => _internalSelectedClientId = clientId;

    private void SetZoom(double delta) =>
        _zoomLevel = Math.Clamp(_zoomLevel + delta, 1, 5);

    private void OnTimelineWheel(WheelEventArgs e)
    {
        var delta = e.DeltaY < 0 ? 0.25 : -0.25;
        SetZoom(delta);
    }

    private void SetFilter(string filter) => _timelineFilter = filter;

    // Mirrors ZombieMatchHistory visual helpers
    private static string FormatTooltipText(ZombieMatchHistoryEvent evt) =>
        $"{evt.Time} • {evt.Label}";

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
}
