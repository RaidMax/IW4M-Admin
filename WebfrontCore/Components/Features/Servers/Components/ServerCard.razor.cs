using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ServerCard
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Parameter] public ServerInfo Model { get; set; }
    [Parameter] public EventCallback<string> OnChat { get; set; }

    
    private PeriodicTimer? _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _showMobileDetails;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Model?.ClientHistory?.ClientCounts != null)
        {
            var strings = new
            {
                players = AppState.Loc("WEBFRONT_SCRIPT_SERVER_PLAYERS"),
                unreachable = AppState.Loc("WEBFRONT_SCRIPT_SERVER_UNREACHABLE")
            };
            // Always update the chart with latest data
            await JS.InvokeVoidAsync("updateServerChart", $"server_history_canvas_{Model.Id}",
                Model.ClientHistory.ClientCounts, Model.MaxClients, strings);
        }

        if (firstRender)
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            RunTimer();
        }
    }

    private async void RunTimer()
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(_cts.Token))
            {
                await Refresh();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[ServerCard] ERROR in RunTimer: {ex}");
        }
    }

    private async Task Refresh()
    {
        try
        {
            var updated = await DataService.GetServer(Model.Id);
            if (updated != null)
            {
                Model = updated;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch
        {
            // Silently handle refresh errors
        }
    }

    private void OpenScoreboard()
    {
        ActionService.OpenCustom(ScoreboardContent(Model.Id), AppState.Loc("WEBFRONT_TITLE_SCOREBOARD"), "max-w-5xl");
    }

    private RenderFragment ScoreboardContent(string serverId) => builder =>
    {
        builder.OpenComponent(0, typeof(ScoreboardModalWrapper));
        builder.AddAttribute(1, nameof(ScoreboardModalWrapper.ServerId), serverId);
        builder.CloseComponent();
    };

    public void Dispose()
    {
        _cts.Cancel();
        _timer?.Dispose();
    }

}
