using Data.Models;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ServerList
{
    [Parameter] public Reference.Game? Game { get; set; }
    private List<ServerInfo>? _servers;
    private Reference.Game? _lastGame;
    [Inject] public required ILogger<ServerList> Logger { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        // Only reload if game filter changed
        if (_servers == null || _lastGame != Game)
        {
            _lastGame = Game;
            try
            {
                _servers = await DataService.GetServersAsync(Game);
            }
            catch (Exception ex)
            {
                Logger.LogError("[ServerList] ERROR in OnParametersSetAsync: {Ex}", ex);
            }
        }
    }

    private void OpenChat(string serverId)
    {
        ActionService.OpenAction("Chat", serverId: serverId, targetId: null, meta: string.Empty);
    }
}
