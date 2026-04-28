using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieMatchHistory
{
    [Parameter, EditorRequired]
    public List<ZombieMatchHistoryMatch> Matches { get; set; } = [];

    [Parameter, EditorRequired]
    public int ClientId { get; set; }

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

    private class MatchCardState
    {
        public bool IsExpanded { get; set; }
    }
}
