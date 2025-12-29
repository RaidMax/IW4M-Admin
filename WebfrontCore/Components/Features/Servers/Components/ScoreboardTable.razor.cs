using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Database.Models;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.Features.Servers.Models;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ScoreboardTable
{
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public ScoreboardInfo Model { get; set; }
    [Parameter] public bool DualColumnMode { get; set; }
    [Parameter] public bool ShowHeader { get; set; } = true;

    private string OrderByKey = nameof(ClientScoreboardInfo.Score);
    private bool Descending = true;

    // Cached team groupings (computed once per render)
    private List<ClientScoreboardInfo>? _sortedClientsCache;
    private List<ClientScoreboardInfo> SortedClientsCache => _sortedClientsCache ??= GetSortedClients().ToList();

    private IEnumerable<ClientScoreboardInfo> AlliesPlayers =>
        SortedClientsCache.Where(c => c.Team == EFClient.TeamType.Allies);

    private IEnumerable<ClientScoreboardInfo> AxisPlayers =>
        SortedClientsCache.Where(c => c.Team == EFClient.TeamType.Axis);

    private IEnumerable<ClientScoreboardInfo> UnknownPlayers =>
        SortedClientsCache.Where(c => c.Team == EFClient.TeamType.Unknown);

    private IEnumerable<ClientScoreboardInfo> SpectatorPlayers =>
        SortedClientsCache.Where(c => c.Team == EFClient.TeamType.Spectator);

    private bool HasTeamPlayers => AlliesPlayers.Any() || AxisPlayers.Any();

    // Team score totals
    private int AlliesTeamScore => AlliesPlayers.Sum(c => c.Score);
    private int AxisTeamScore => AxisPlayers.Sum(c => c.Score);

    protected override void OnParametersSet()
    {
        // Clear cache when Model changes (for live updates)
        _sortedClientsCache = null;
    }

    private IEnumerable<ClientScoreboardInfo> GetSortedClients()
    {
        if (Model?.ClientInfo == null) return [];

        Func<ClientScoreboardInfo, object> keySelector = OrderByKey switch
        {
            nameof(ClientScoreboardInfo.ClientName) => c => c.ClientName ?? "",
            nameof(ClientScoreboardInfo.Kills) => c => c.Kills ?? 0,
            nameof(ClientScoreboardInfo.Deaths) => c => c.Deaths ?? 0,
            nameof(ClientScoreboardInfo.Kdr) => c => c.Kdr ?? 0,
            nameof(ClientScoreboardInfo.ScorePerMinute) => c => c.ScorePerMinute ?? 0,
            nameof(ClientScoreboardInfo.ZScore) => c => c.ZScore ?? 0,
            nameof(ClientScoreboardInfo.Ping) => c => c.Ping,
            _ => c => c.Score
        };

        return Descending
            ? Model.ClientInfo.OrderByDescending(keySelector)
            : Model.ClientInfo.OrderBy(keySelector);
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

        // Clear cache to re-sort
        _sortedClientsCache = null;
    }

    private MarkupString GetSortIndicator(string key)
    {
        if (OrderByKey == key)
        {
            return new MarkupString(
                Descending ? "<span class=\"ml-1 text-[10px]\">▼</span>" : "<span class=\"ml-1 text-[10px]\">▲</span>");
        }

        return new MarkupString(string.Empty);
    }

    private string GetTeamColorClass(EFClient.TeamType team) => team switch
    {
        EFClient.TeamType.Allies => "text-sky-400",
        EFClient.TeamType.Axis => "text-rose-400",
        EFClient.TeamType.Spectator => "text-slate-500",
        _ => "text-muted"
    };

    private string GetTeamBorderClass(EFClient.TeamType team) => team switch
    {
        EFClient.TeamType.Allies => "border-sky-500/30",
        EFClient.TeamType.Axis => "border-rose-500/30",
        _ => "border-line"
    };
}
