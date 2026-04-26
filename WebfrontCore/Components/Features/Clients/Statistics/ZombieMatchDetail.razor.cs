using Microsoft.AspNetCore.Components;
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

    private void OnFilterChanged(string filter) => _timelineFilter = filter;
    private void OnZoomLevelChanged(double zoom) => _zoomLevel = zoom;
}
