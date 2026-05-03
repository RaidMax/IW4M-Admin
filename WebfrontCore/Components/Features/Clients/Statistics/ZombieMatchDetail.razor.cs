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

    /// <summary>
    /// Forwarded to <see cref="ZombieMatchScrubber"/> so the consumer's SHOW_ALL
    /// toggle (e.g. the leaderboard card) governs both scoreboard rows AND timeline
    /// lane visibility from a single control. Default false (qualified-only).
    /// Standalone share page (<c>ZombieMatchPage</c>) sets this true so a deep-link
    /// to a match shows the full roster.
    /// </summary>
    [Parameter]
    public bool ShowAllPlayers { get; set; }

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
                _memoizedPayload = ZombieScrubberPayload.From(Detail, AppState.Loc);
                _memoizedDetailKey = Detail;
            }
            return _memoizedPayload;
        }
    }

    private int ActiveClientId => SelectedClientId ?? _internalSelectedClientId;

    /// <summary>
    /// Players to surface in the tab bar. When the consumer hides drop-ins
    /// (<see cref="ShowAllPlayers"/> = false), unqualified lanes also drop from
    /// the tabs so the tab bar matches the timeline lanes — same control, same
    /// visible roster. The active selection auto-falls-back to the first
    /// visible player if the previously-active one just hid.
    /// </summary>
    private IEnumerable<ZombieMatchDetailPlayer> VisiblePlayers =>
        ShowAllPlayers ? Detail.Players : Detail.Players.Where(p => p.IsQualified);

    protected override void OnParametersSet()
    {
        // Active player must always be in the visible set; if the SHOW_ALL
        // toggle just hid the previously-active drop-in, snap to the first
        // visible qualifier so the panel never renders an empty body.
        var visible = VisiblePlayers.ToList();
        if (visible.Count > 0 && visible.All(p => p.ClientId != ActiveClientId))
        {
            _internalSelectedClientId = visible[0].ClientId;
        }
    }

    private ZombieMatchDetailPlayer? SelectedPlayer =>
        VisiblePlayers.FirstOrDefault(p => p.ClientId == ActiveClientId);

    private void SelectPlayer(int clientId) => _internalSelectedClientId = clientId;
}
