using Microsoft.AspNetCore.Components;
using WebfrontCore.Controllers.API;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class Help
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private List<CommandGroupDto> CommandGroups { get; set; }
    private string CommandPrefix { get; set; } = "!";

    protected override async Task OnInitializedAsync()
    {
        CommandGroups = await Api.GetHelpAsync();
        // Try to get command prefix from status API
        try
        {
            var status = await Api.GetStatusAsync();
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
