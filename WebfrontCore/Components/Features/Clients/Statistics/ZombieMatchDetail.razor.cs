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

    // Memoized to keep reference stable across renders — scrubber treats payload
    // identity change as a full reinit (which discards zoom/scroll). Recomputed
    // only when Detail itself changes.
    private SharedLibraryCore.Interfaces.ZombieMatchDetail? _memoizedDetailKey;
    private ZombieScrubberPayload? _memoizedPayload;

    private ZombieScrubberPayload ScrubberPayload
    {
        get
        {
            if (!ReferenceEquals(_memoizedDetailKey, Detail) || _memoizedPayload is null)
            {
                _memoizedPayload = ZombieScrubberPayload.From(Detail);
                _memoizedDetailKey = Detail;
            }
            return _memoizedPayload;
        }
    }

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
}
