using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Servers.Pages;

public partial class ScoreboardIndex
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    private bool Loading { get; set; } = true;
    private bool NoServers { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var servers = await Api.GetServersAsync();
            if (servers != null && servers.Count != 0)
            {
                // Redirect to first server's scoreboard
                NavManager.NavigateTo($"/scoreboard/{servers.First().Id}", replace: true);
                return;
            }

            NoServers = true;
        }
        catch
        {
            NoServers = true;
        }
        finally
        {
            Loading = false;
        }
    }
}
