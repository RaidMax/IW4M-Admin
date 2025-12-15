using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Shared;

public partial class ServerCard
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Parameter] public ServerInfo Model { get; set; }
    private PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    
    private bool _chartInitialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Model?.ClientHistory?.ClientCounts != null && !_chartInitialized)
        {
            var strings = new { players = AppState.Loc("WEBFRONT_SCRIPT_SERVER_PLAYERS"), unreachable = AppState.Loc("WEBFRONT_SCRIPT_SERVER_UNREACHABLE") };
            await JS.InvokeVoidAsync("initServerChart", $"server_history_canvas_{Model.ID}", Model.ClientHistory.ClientCounts, Model.MaxClients, strings);
            _chartInitialized = true;
        }

        if (firstRender)
        {
            // Start timer only after render
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            RunTimer();
        }
    }

    private async void RunTimer()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                await Refresh();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task Refresh()
    {
        try
        {
            var updated = await Api.GetServerAsync(Model.ID);
            if (updated != null)
            {
                // Update properties only if meaningful to avoid full re-render flickering? 
                // Or just replace model.
                Model = updated;
                StateHasChanged();
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer?.Dispose();
    }
}
