using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.Features.Servers.Models;

namespace WebfrontCore.Components.Features.Servers.Pages;

public partial class ServerScoreboard
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public string Id { get; set; }
    private ScoreboardInfo ScoreboardModel { get; set; }
    private SideContextMenuItems ContextItems { get; set; }
    private PeriodicTimer _refreshTimer;
    private CancellationTokenSource _cts;
    private string _previousId;

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
                        Reference = $"/scoreboard/{server.Id}",
                        Title = server.Name,
                        IsActive = server.Id == Id,
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
