using Microsoft.AspNetCore.Components;
using WebfrontCore.Services;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Components.Pages.Server;

public partial class Scoreboard
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public long Id { get; set; }
    private ScoreboardInfo ScoreboardModel { get; set; }
    private SideContextMenuItems ContextItems { get; set; }
    private PeriodicTimer _refreshTimer;
    private CancellationTokenSource _cts;
    private long _previousId;

    protected override async Task OnParametersSetAsync()
    {
        // If ID changed, reset and reload
        if (_previousId != Id)
        {
            _previousId = Id;
            ScoreboardModel = null; // Reset to show loader on ID change
            ContextItems = null;

            // Cancel previous timer if running
            _cts?.Cancel();
            _cts?.Dispose();
            _refreshTimer?.Dispose();

            await LoadDataAsync();

            // Start refresh timer
            _cts = new CancellationTokenSource();
            _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            _ = RefreshScoreboardAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Fetch the specific server scoreboard
            ScoreboardModel = await Api.GetScoreboardAsync(Id);

            // Only load context menu once (server list doesn't change often)
            if (ContextItems == null)
            {
                var servers = await Api.GetServersAsync();

                ContextItems = new SideContextMenuItems
                {
                    MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_SERVER"),
                    Items = servers.Select(server => new SideContextMenuItem
                    {
                        IsLink = true,
                        Reference = $"/Server/{server.ID}/Scoreboard",
                        Title = server.Name,
                        IsActive = server.ID == Id,
                        IsCollapse = true,
                        Meta = server.Game.ToString()
                    }).ToList()
                };
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
        }
    }

    private async Task RefreshScoreboardAsync()
    {
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    ScoreboardModel = await Api.GetScoreboardAsync(Id);
                    await InvokeAsync(StateHasChanged);
                }
                catch
                {
                    // Ignore refresh errors
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when component is disposed or ID changes
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _refreshTimer?.Dispose();
    }
}
