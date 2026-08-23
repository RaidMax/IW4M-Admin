using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;

namespace LiveRadar.Components;

public partial class LiveRadar
{
    [Parameter] public string ServerId { get; set; }

    private List<Server> Servers { get; set; } = [];

    private string SelectedServerName =>
        Servers.FirstOrDefault(s => s.ToString() == ServerId)?.Hostname ?? "Unknown Server";

    protected override void OnInitialized()
    {
        Servers = Manager.GetServers()
            .Where(server => server.GameName is Server.Game.IW4 or Server.Game.D7D)
            .ToList();

        if (string.IsNullOrEmpty(ServerId) && Servers.Any())
        {
            ServerId = Servers.First().ToString();
        }
    }

    private string _initializedServerId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var shouldInit = false;
        if (!string.IsNullOrEmpty(ServerId))
        {
            if (firstRender)
                shouldInit = true;
            if (ServerId != _initializedServerId)
                shouldInit = true;
        }

        if (shouldInit)
        {
            _initializedServerId = ServerId;
            var radarUrl = $"/Radar/{ServerId}/Data";
            var mapUrl = $"/Radar/{ServerId}/Map";
            try
            {
                await JS.InvokeVoidAsync("initLiveRadar", radarUrl, mapUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LiveRadar] JS Invoke Failed: {ex.Message}");
            }
        }
    }

    private bool IsSelected(Server server)
    {
        return server.ToString() == ServerId;
    }

    private bool _mobileMenuOpen;

    private void ToggleMobileServerMenu()
    {
        _mobileMenuOpen = !_mobileMenuOpen;
        StateHasChanged();
    }
}
