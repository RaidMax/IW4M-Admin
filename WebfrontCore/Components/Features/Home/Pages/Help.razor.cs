using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Components.Features.Console.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class Help
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private List<CommandGroupInfo>? CommandGroups { get; set; }
    private string CommandPrefix { get; set; } = "!";
    
    // State for accordion expansion
    private HashSet<string> ExpandedGroups { get; set; } = new();
    
    // State for permission filtering
    private HashSet<Data.Models.Client.EFClient.Permission> SelectedPermissions { get; set; } = new();
    
    // Available permission levels for filter chips
    private static readonly Data.Models.Client.EFClient.Permission[] AvailablePermissions =
    [
        Data.Models.Client.EFClient.Permission.User,
        Data.Models.Client.EFClient.Permission.Trusted,
        Data.Models.Client.EFClient.Permission.Moderator,
        Data.Models.Client.EFClient.Permission.Administrator,
        Data.Models.Client.EFClient.Permission.SeniorAdmin,
        Data.Models.Client.EFClient.Permission.Owner
    ];

    private string _searchTerm = string.Empty;
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
            if (CommandGroups == null)
            {
                return Enumerable.Empty<CommandGroupInfo>();
            }

            var groups = CommandGroups.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                
                groups = groups
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
                    .Where(group => group.Commands.Any());
            }

            // Apply permission filter
            if (SelectedPermissions.Any())
            {
                groups = groups
                    .Select(group => new CommandGroupInfo
                    {
                        Name = group.Name,
                        Commands = group.Commands.Where(c => 
                            SelectedPermissions.Contains(c.Permission)
                        ).ToList()
                    })
                    .Where(group => group.Commands.Any());
            }

            return groups.ToList();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        CommandGroups = await DataService.GetHelpCommandsAsync();
        
        // Expand first group by default for better UX
        if (CommandGroups?.Any() == true)
        {
            ExpandedGroups.Add(CommandGroups.First().Name);
        }
        
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

    private void ToggleGroup(string groupName)
    {
        if (ExpandedGroups.Contains(groupName))
        {
            ExpandedGroups.Remove(groupName);
        }
        else
        {
            ExpandedGroups.Add(groupName);
        }
    }

    private void TogglePermissionFilter(Data.Models.Client.EFClient.Permission permission)
    {
        if (SelectedPermissions.Contains(permission))
        {
            SelectedPermissions.Remove(permission);
        }
        else
        {
            SelectedPermissions.Add(permission);
        }
    }

    private void ClearPermissionFilters()
    {
        SelectedPermissions.Clear();
    }

    private async Task CopyToClipboard(string text)
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }

    private string GetGroupIcon(string groupName) => groupName.ToLowerInvariant() switch
    {
        var n when n.Contains("admin") => "ph-shield-star",
        var n when n.Contains("moderat") => "ph-gavel",
        var n when n.Contains("user") => "ph-user",
        var n when n.Contains("stat") => "ph-chart-bar",
        var n when n.Contains("server") => "ph-hard-drive",
        var n when n.Contains("client") || n.Contains("player") => "ph-users",
        var n when n.Contains("kick") || n.Contains("ban") => "ph-prohibit",
        var n when n.Contains("map") => "ph-map-trifold",
        var n when n.Contains("vote") => "ph-thumbs-up",
        var n when n.Contains("message") || n.Contains("chat") => "ph-chat-text",
        _ => "ph-command"
    };

    private string GetPermissionChipClass(Data.Models.Client.EFClient.Permission permission, bool isActive) =>
        permission switch
        {
            Data.Models.Client.EFClient.Permission.User when isActive => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-slate-500 text-white border border-slate-400 transition-all",
            Data.Models.Client.EFClient.Permission.User => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-slate-400 border border-line hover:border-slate-400 transition-all",
            
            Data.Models.Client.EFClient.Permission.Trusted when isActive => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-green-600 text-white border border-green-500 transition-all",
            Data.Models.Client.EFClient.Permission.Trusted => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-green-500 border border-line hover:border-green-500 transition-all",
            
            Data.Models.Client.EFClient.Permission.Moderator when isActive => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-yellow-600 text-white border border-yellow-500 transition-all",
            Data.Models.Client.EFClient.Permission.Moderator => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-yellow-500 border border-line hover:border-yellow-500 transition-all",
            
            Data.Models.Client.EFClient.Permission.Administrator when isActive => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-pink-500 text-white border border-pink-400 transition-all",
            Data.Models.Client.EFClient.Permission.Administrator => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-pink-400 border border-line hover:border-pink-400 transition-all",
            
            Data.Models.Client.EFClient.Permission.SeniorAdmin when isActive => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-cyan-600 text-white border border-cyan-500 transition-all",
            Data.Models.Client.EFClient.Permission.SeniorAdmin => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-cyan-500 border border-line hover:border-cyan-500 transition-all",
            
            Data.Models.Client.EFClient.Permission.Owner when isActive => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-blue-600 text-white border border-blue-500 transition-all",
            Data.Models.Client.EFClient.Permission.Owner => 
                "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-blue-500 border border-line hover:border-blue-500 transition-all",
            
            _ when isActive => "px-3 py-1.5 text-xs font-medium rounded-full bg-gray-500 text-white border border-gray-400 transition-all",
            _ => "px-3 py-1.5 text-xs font-medium rounded-full bg-surface-alt text-gray-400 border border-line hover:border-gray-400 transition-all"
        };

    private string GetPermissionBadgeClass(Data.Models.Client.EFClient.Permission permission) => permission switch
    {
        Data.Models.Client.EFClient.Permission.User => "bg-slate-500/10 text-slate-400 border border-slate-500/20",
        Data.Models.Client.EFClient.Permission.Trusted => "bg-green-500/10 text-green-500 border border-green-500/20",
        Data.Models.Client.EFClient.Permission.Moderator => "bg-yellow-500/10 text-yellow-500 border border-yellow-500/20",
        Data.Models.Client.EFClient.Permission.Administrator => "bg-pink-500/10 text-pink-400 border border-pink-500/20",
        Data.Models.Client.EFClient.Permission.SeniorAdmin => "bg-cyan-500/10 text-cyan-500 border border-cyan-500/20",
        Data.Models.Client.EFClient.Permission.Owner => "bg-blue-500/10 text-blue-500 border border-blue-500/20",
        Data.Models.Client.EFClient.Permission.Console => "bg-red-500/10 text-red-500 border border-red-500/20",
        _ => "bg-gray-500/10 text-gray-400 border border-gray-500/20"
    };
}
