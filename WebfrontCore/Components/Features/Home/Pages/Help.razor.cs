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

    private string _searchTerm;
    private string SearchTerm
    {
        get => _searchTerm;
        set
        {
            _searchTerm = value;
            StateHasChanged();
        }
    }

    private IEnumerable<CommandGroupInfo> FilteredCommandGroups
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                return CommandGroups;
            }

            var term = SearchTerm.Trim();
            
            return CommandGroups
                .Select(group => new CommandGroupInfo
                {
                    Name = group.Name,
                    Commands = group.Commands.Where(c => 
                        c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        c.Alias.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        c.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        c.Syntax.Contains(term, StringComparison.OrdinalIgnoreCase)
                    ).ToList()
                })
                .Where(group => group.Commands.Any())
                .ToList();
        }
    }

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

    private string GetLevelColorClass(Data.Models.Client.EFClient.Permission permission) => permission switch
    {
        Data.Models.Client.EFClient.Permission.User => "text-slate-500 dark:text-slate-400",
        Data.Models.Client.EFClient.Permission.Trusted => "text-green-600 dark:text-green-500",
        Data.Models.Client.EFClient.Permission.Moderator => "text-yellow-600 dark:text-yellow-500",
        Data.Models.Client.EFClient.Permission.Administrator => "text-pink-500 dark:text-pink-400",
        Data.Models.Client.EFClient.Permission.SeniorAdmin => "text-cyan-600 dark:text-cyan-500",
        Data.Models.Client.EFClient.Permission.Owner => "text-blue-600 dark:text-blue-500",
        Data.Models.Client.EFClient.Permission.Console => "text-red-600 dark:text-red-500",
        _ => "text-gray-500"
    };
}
