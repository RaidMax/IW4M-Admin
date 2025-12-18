using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Features.Console.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class Help
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private List<CommandGroupInfo> CommandGroups { get; set; }
    private string CommandPrefix { get; set; } = "!";

    protected override async Task OnInitializedAsync()
    {
        CommandGroups = await DataService.GetHelpCommandsAsync();
        // Try to get command prefix from status API
        try
        {
            var status = await DataService.GetStatusAsync();
            if (!string.IsNullOrEmpty(status?.CommandPrefix))
            {
                CommandPrefix = status.CommandPrefix;
            }
        }
        catch
        {
            // Use default prefix if status API fails
        }
    }
}
