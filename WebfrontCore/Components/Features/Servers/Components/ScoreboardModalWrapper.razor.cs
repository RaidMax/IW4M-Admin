using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ScoreboardModalWrapper
{
    [Parameter] public string ServerId { get; set; }
    [Inject] public required IWebfrontDataService ServerDataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private bool _isLoading = true;
    private string? _error;
    private ScoreboardInfo? _scoreboardInfo;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }
        
        try
        {
            _scoreboardInfo = await ServerDataService.GetServerScoreboardAsync(ServerId);
        }
        catch (Exception ex)
        {
            _error = AppState.Loc("WEBFRONT_SCOREBOARD_ERROR_LOADING");
            System.Console.WriteLine(ex);
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}