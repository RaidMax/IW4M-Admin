using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieRoundTable
{
    [Parameter, EditorRequired]
    public List<ZombieMatchHistoryRound> Rounds { get; set; } = [];

    /// <summary>
    /// Optional: the match's overall HighestRound. When supplied AND the player's last
    /// tracked round is below it, a "left early" gap row is rendered at the bottom.
    /// Without this, the table can only show internal gaps + "joined late" gaps.
    /// </summary>
    [Parameter] public int? MatchHighestRound { get; set; }

    private static string FormatRoundTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
