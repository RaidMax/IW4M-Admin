using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;


namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientActivity
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Parameter] public ServerInfo? Model { get; set; }
    
    private List<ClientGroup> GroupedClients => GetGroupedClients();

    public class ClientGroup
    {
        public int Index { get; set; }
        public List<ClientItem> Group { get; set; } = [];
    }

    public class ClientItem
    {
        public int Index { get; set; }
        public PlayerInfo Client { get; set; }
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
            "CONNECTED" => "ph ph-sign-in text-success mr-5",
            "DISCONNECTED" => "ph ph-sign-out text-danger mr-5",
            _ => ""
        };
    }

    private string CapClientName(string? message, int length)
    {
        if (message == null) return string.Empty;
        return message.Length > length ? message[..length] + "..." : message;
    }
}
