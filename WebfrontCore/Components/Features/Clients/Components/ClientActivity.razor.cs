using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;


namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientActivity
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Parameter] public ServerInfo? Model { get; set; }

    /// <summary>
    /// Render the chat/event feed column alongside the scoreboard. When false only the scoreboard is shown.
    /// </summary>
    [Parameter] public bool ShowChat { get; set; } = true;

    private List<ClientGroup> GroupedClients => GetGroupedClients();

    private static readonly SharedLibraryCore.Database.Models.EFClient.TeamType[] ScoreboardTeams =
    [
        SharedLibraryCore.Database.Models.EFClient.TeamType.Allies,
        SharedLibraryCore.Database.Models.EFClient.TeamType.Axis
    ];

    /// <summary>
    /// True when the lobby has players on both teams, so the board can be split like the in-game scoreboard.
    /// </summary>
    private bool HasTeams => Model?.Players is not null &&
                             ScoreboardTeams.All(team => Model.Players.Any(player => player.Team == team));

    private IEnumerable<PlayerInfo> TeamPlayers(SharedLibraryCore.Database.Models.EFClient.TeamType team) =>
        Model!.Players!.Where(player => player.Team == team).OrderByDescending(player => player.Score);

    private IEnumerable<PlayerInfo> UnassignedPlayers() =>
        Model!.Players!.Where(player => !ScoreboardTeams.Contains(player.Team)).OrderByDescending(player => player.Score);

    private int TeamScore(SharedLibraryCore.Database.Models.EFClient.TeamType team) =>
        TeamPlayers(team).Sum(player => player.Score ?? 0);

    private string TeamLabel(SharedLibraryCore.Database.Models.EFClient.TeamType team)
    {
        var name = TeamPlayers(team).Select(player => player.TeamName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return string.IsNullOrWhiteSpace(name) ? team.ToString() : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string TeamColour(SharedLibraryCore.Database.Models.EFClient.TeamType team) =>
        team == SharedLibraryCore.Database.Models.EFClient.TeamType.Allies ? "text-sky-400" : "text-rose-400";

    private static string TeamIcon(SharedLibraryCore.Database.Models.EFClient.TeamType team) =>
        team == SharedLibraryCore.Database.Models.EFClient.TeamType.Allies ? "ph-shield-check" : "ph-shield-warning";

    public class ClientGroup
    {
        public int Index { get; set; }
        public List<ClientItem> Group { get; set; } = [];
    }

    public class ClientItem
    {
        public int Index { get; set; }
        public required PlayerInfo Client { get; set; }
    }

    private List<ClientGroup> GetGroupedClients()
    {
        if (Model == null) return [];
        var half = Model.ClientCount == 0 || Model.Players.Count == 0 ? 0 : (int)Math.Ceiling(Model.ClientCount / 2.0);

        return Model.Players
            .Select((client, i) => new ClientItem { Index = i, Client = client })
            .OrderBy(c => c.Client.Name)
            .GroupBy(c => c.Index >= half)
            .Select((group, index) => new ClientGroup
            {
                Index = index,
                Group = group.ToList()
            }).ToList();
    }

    private string GetIconForState(string messageState)
    {
        return messageState switch
        {
            "CONNECTED" => "ph-bold ph-sign-in text-emerald-500",
            "DISCONNECTED" => "ph-bold ph-sign-out text-rose-500",
            _ => ""
        };
    }

    private (string Icon, string Color) GetPingIconAndColor(int ping)
    {
        var (icon, color) = ping switch
        {
            < 50 => ("ph-fill ph-cell-signal-full", "text-emerald-500"),
            < 100 => ("ph-fill ph-cell-signal-high", "text-lime-500"),
            < 150 => ("ph-fill ph-cell-signal-medium", "text-amber-500"),
            < 200 => ("ph-fill ph-cell-signal-low", "text-orange-500"),
            _ => ("ph-fill ph-cell-signal-none", "text-red-500")
        };
        return (icon, color);
    }

    private string CapClientName(string? message, int length)
    {
        if (message == null) return string.Empty;
        return message.Length > length ? message[..length] + "..." : message;
    }
}
