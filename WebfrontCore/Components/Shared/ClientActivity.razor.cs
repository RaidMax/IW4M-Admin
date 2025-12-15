using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Shared;

public partial class ClientActivity
{
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public ServerInfo? Model { get; set; }
    
    // TODO: dynamic? Yuck. WHAT IS THAT!?
    private List<dynamic> GroupedClients => GetGroupedClients();

    private List<dynamic> GetGroupedClients()
    {
        if (Model == null) return [];
        var half = Model.ClientCount == 0 || Model.Players.Count == 0 ? 0 : (int)Math.Ceiling(Model.ClientCount / 2.0);
        return Model.Players.Select((client, i) => new { index = i, client })
            .OrderBy(c => c.client.Name)
            .GroupBy(c => c.index >= half).Select((group, index) => (dynamic)new
            {
                group,
                index
            }).ToList();
    }

    private string GetIconForState(string messageState)
    {
        return messageState switch
        {
            "CONNECTED" => "oi-account-login text-success mr-5",
            "DISCONNECTED" => "oi-account-logout text-danger mr-5",
            _ => ""
        };
    }

    private string CapClientName(string? message, int length)
    {
        if (message == null) return string.Empty;
        return message.Length > length ? message[..length] + "..." : message;
    }
}
