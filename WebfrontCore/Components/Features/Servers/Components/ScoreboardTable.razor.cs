using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Database.Models;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.Features.Servers.Models;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ScoreboardTable
{
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public ScoreboardInfo Model { get; set; }
    private string OrderByKey = nameof(ClientScoreboardInfo.Score);
    private bool Descending = true;
    private IEnumerable<ClientScoreboardInfo> SortedClients => GetSortedClients();

    private IEnumerable<ClientScoreboardInfo> GetSortedClients()
    {
        var query = Model.ClientInfo.AsQueryable(); // Just to use dynamic OrderBy if needed, or switch

        // Simple switch for now
        Func<ClientScoreboardInfo, object> keySelector = OrderByKey switch
        {
            nameof(ClientScoreboardInfo.ClientName) => c => c.ClientName,
            nameof(ClientScoreboardInfo.Kills) => c => c.Kills,
            nameof(ClientScoreboardInfo.Deaths) => c => c.Deaths,
            nameof(ClientScoreboardInfo.Kdr) => c => c.Kdr,
            nameof(ClientScoreboardInfo.ScorePerMinute) => c => c.ScorePerMinute,
            nameof(ClientScoreboardInfo.ZScore) => c => c.ZScore,
            nameof(ClientScoreboardInfo.Ping) => c => c.Ping,
            _ => c => c.Score
        };

        return Descending ? Model.ClientInfo.OrderByDescending(keySelector) : Model.ClientInfo.OrderBy(keySelector);
    }

    private void Sort(string key)
    {
        if (OrderByKey == key)
        {
            Descending = !Descending;
        }
        else
        {
            OrderByKey = key;
            Descending = true;
        }
    }

    private MarkupString GetSortIndicator(string key)
    {
        if (OrderByKey == key)
        {
            return new MarkupString(
                Descending ? "<span class=\"ml-5 font-size-12\">▼</span>" : "<span class=\"ml-5 font-size-12\">▲</span>");
        }

        return new MarkupString(string.Empty);
    }

    private string GetTeamBackgroundColorClass(ClientScoreboardInfo client)
    {
         return client.Team.ToString() switch
        {
            "Axis" => "bg-rose-900/10 hover:bg-rose-900/20",
            "Allies" => "bg-sky-900/10 hover:bg-sky-900/20",
            "Spectator" => "bg-slate-700/30 hover:bg-slate-700/50",
            _ => "hover:bg-slate-700/30"
        };
    }
}
