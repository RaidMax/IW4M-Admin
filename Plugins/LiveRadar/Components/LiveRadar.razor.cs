using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.LiveRadar;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.LiveRadar.Components;

public partial class LiveRadar : IAsyncDisposable
{
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required LiveRadarConfiguration Config { get; set; }

    [Parameter] public string ServerId { get; set; }

    // pushed to the client on this cadence; the JS interpolates between snapshots over this window.
    private const int UpdateFrequencyMs = 750;

    private List<Server> Servers { get; set; } = [];
    private string _initializedServerId;
    private CancellationTokenSource _loopCts;

    private string SelectedServerName =>
        Servers.FirstOrDefault(s => s.ToString() == ServerId)?.Hostname ?? "Unknown Server";

    protected override void OnInitialized()
    {
        Servers = Manager.GetServers()
            .Where(server => server.GameName == Server.Game.IW4)
            .ToList();

        if (string.IsNullOrEmpty(ServerId) && Servers.Any())
        {
            ServerId = Servers.First().ToString();
        }
    }

    // server selector rendered in the page shell's side menu (replaces the old bespoke sidebar)
    private SideContextMenuItems BuildNav() => new()
    {
        MenuTitle = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_LIVE_RADAR_TITLE"],
        Items = Servers.Select(server => new SideContextMenuItem
        {
            IsLink = true,
            Title = server.Hostname.StripColors(),
            Reference = $"/radar/{server}",
            Icon = "ph-fill ph-hard-drive",
            IsActive = server.ToString() == ServerId
        }).ToList()
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (string.IsNullOrEmpty(ServerId))
        {
            return;
        }

        var serverChanged = ServerId != _initializedServerId;
        if (!firstRender && !serverChanged)
        {
            return;
        }

        _initializedServerId = ServerId;

        try
        {
            // Load the bundle's script as an ES module before first interop (the browser caches the
            // module by URL, so re-running this on server switch is a no-op). It publishes
            // window.initLiveRadar / setMapData / setRadarData, which the calls below target.
            await JS.InvokeAsync<IJSObjectReference>("import", "/_content/liveradar/liveradar.js");
            // (re)initialize the canvas/state, then paint immediately so the user isn't waiting a full tick
            await JS.InvokeVoidAsync("initLiveRadar");
            await PushSnapshotAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiveRadar] JS init failed: {ex.Message}");
        }

        if (firstRender)
        {
            _loopCts = new CancellationTokenSource();
            _ = RunPushLoopAsync(_loopCts.Token);
        }
    }

    // server-side replacement for the browser's old HTTP polling: push map + player snapshots over the
    // Blazor circuit on a timer. removes the need for the radar's MVC controller entirely.
    private async Task RunPushLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateFrequencyMs));
            while (await timer.WaitForNextTickAsync(token))
            {
                await PushSnapshotAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // page closed
        }
    }

    private async Task PushSnapshotAsync()
    {
        var server = Servers.FirstOrDefault(s => s.ToString() == ServerId);
        if (server is null)
        {
            return;
        }

        var map = Config.Maps?.FirstOrDefault(m => m.Name == server.CurrentMap?.Name);
        if (map is not null)
        {
            map.Alias = server.CurrentMap.Alias;
        }

        var players = server.GetClientsAsList()
            .Select(client => client.GetAdditionalProperty<RadarDto>("LiveRadar"))
            .ToList();

        try
        {
            // Blazor JS interop serializes these camelCase, matching the field names the canvas script reads
            if (map is not null)
            {
                await JS.InvokeVoidAsync("setMapData", map);
            }

            await JS.InvokeVoidAsync("setRadarData", players);
        }
        catch
        {
            // circuit tearing down between the timer tick and the invoke
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            _loopCts.Dispose();
        }
    }
}
