using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using System.Web;
using WebfrontCore.Components.Features.Console.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class Help
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private List<CommandGroupInfo>? CommandGroups { get; set; }
    private string CommandPrefix { get; set; } = "!";

    // View mode toggle
    public enum ViewModeType
    {
        Cards,
        Table
    }

    private ViewModeType ViewMode { get; set; } = ViewModeType.Table;

    // State for accordion expansion (card view)
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

            // Apply fuzzy search filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();

                groups = groups
                    .Select(group => new CommandGroupInfo
                    {
                        Name = group.Name,
                        Commands = group.Commands.Where(c =>
                            FuzzyMatch(c.Name, term) ||
                            FuzzyMatch(c.Alias, term) ||
                            FuzzyMatch(c.Description, term) ||
                            FuzzyMatch(c.Syntax, term) ||
                            FuzzyMatch(c.Permission.ToLocalizedLevelName(), term)
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

    /// <summary>
    /// Fuzzy string matching - supports partial matches, word boundaries, and typo tolerance
    /// </summary>
    private static bool FuzzyMatch(string? source, string searchTerm)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(searchTerm))
            return false;

        var sourceLower = source.ToLowerInvariant();

        // Exact substring match (highest priority)
        if (sourceLower.Contains(searchTerm))
            return true;

        // Word starts-with match (e.g., "ban" matches "tempban", "banuser")
        var words = sourceLower.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => word.StartsWith(searchTerm)))
            return true;

        // Fuzzy character sequence match (characters appear in order, with gaps allowed)
        // e.g., "tmpbn" matches "tempban"
        if (searchTerm.Length >= 4 && FuzzySequenceMatch(sourceLower, searchTerm))
            return true;

        // Levenshtein distance for short terms (typo tolerance)
        // Only use for small words to avoid false positives
        if (searchTerm.Length is >= 4 and <= 8)
        {
            // Check each word in the source
            if (words.Any(word =>
                    word.Length >= searchTerm.Length - 2 &&
                    word.Length <= searchTerm.Length + 2 &&
                    LevenshteinDistance(word, searchTerm) <= 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if all characters in search term appear in source in order
    /// </summary>
    private static bool FuzzySequenceMatch(string source, string searchTerm)
    {
        var sourceIndex = 0;
        var matchCount = 0;

        foreach (var c in searchTerm)
        {
            while (sourceIndex < source.Length)
            {
                if (source[sourceIndex] == c)
                {
                    matchCount++;
                    sourceIndex++;
                    break;
                }

                sourceIndex++;
            }
        }

        // Require at least 80% of characters to match in sequence
        return matchCount >= searchTerm.Length * 0.8;
    }

    /// <summary>
    /// Compute Levenshtein edit distance between two strings
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;

        // Use a single-row optimization for memory efficiency
        var previousRow = new int[targetLength + 1];
        var currentRow = new int[targetLength + 1];

        for (var j = 0; j <= targetLength; j++)
            previousRow[j] = j;

        for (var i = 1; i <= sourceLength; i++)
        {
            currentRow[0] = i;

            for (var j = 1; j <= targetLength; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[targetLength];
    }

    protected override async Task OnInitializedAsync()
    {
        CommandGroups = await DataService.GetHelpCommandsAsync();

        // Expand first group by default for better UX (card view)
        if (CommandGroups.Count != 0)
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Set default view to Cards on mobile
            try
            {
                var isMobile = await JS.InvokeAsync<bool>("eval", "window.innerWidth < 768");
                if (isMobile && ViewMode == ViewModeType.Table)
                {
                    ViewMode = ViewModeType.Cards;
                    StateHasChanged();
                }
            }
            catch
            {
                // Fallback to default if JS interop fails
            }
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
                "px-4 py-2 text-sm font-medium rounded-lg bg-slate-500 text-white shadow-lg transition-all duration-200",
            Data.Models.Client.EFClient.Permission.User =>
                "px-4 py-2 text-sm font-medium rounded-lg text-slate-400 hover:bg-slate-500/10 transition-all duration-200",

            Data.Models.Client.EFClient.Permission.Trusted when isActive =>
                "px-4 py-2 text-sm font-medium rounded-lg bg-green-600 text-white shadow-lg transition-all duration-200",
            Data.Models.Client.EFClient.Permission.Trusted =>
                "px-4 py-2 text-sm font-medium rounded-lg text-green-500 hover:bg-green-500/10 transition-all duration-200",

            Data.Models.Client.EFClient.Permission.Moderator when isActive =>
                "px-4 py-2 text-sm font-medium rounded-lg bg-yellow-600 text-white shadow-lg transition-all duration-200",
            Data.Models.Client.EFClient.Permission.Moderator =>
                "px-4 py-2 text-sm font-medium rounded-lg text-yellow-500 hover:bg-yellow-500/10 transition-all duration-200",

            Data.Models.Client.EFClient.Permission.Administrator when isActive =>
                "px-4 py-2 text-sm font-medium rounded-lg bg-pink-500 text-white shadow-lg transition-all duration-200",
            Data.Models.Client.EFClient.Permission.Administrator =>
                "px-4 py-2 text-sm font-medium rounded-lg text-pink-400 hover:bg-pink-500/10 transition-all duration-200",

            Data.Models.Client.EFClient.Permission.SeniorAdmin when isActive =>
                "px-4 py-2 text-sm font-medium rounded-lg bg-cyan-600 text-white shadow-lg transition-all duration-200",
            Data.Models.Client.EFClient.Permission.SeniorAdmin =>
                "px-4 py-2 text-sm font-medium rounded-lg text-cyan-500 hover:bg-cyan-500/10 transition-all duration-200",

            Data.Models.Client.EFClient.Permission.Owner when isActive =>
                "px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white shadow-lg transition-all duration-200",
            Data.Models.Client.EFClient.Permission.Owner =>
                "px-4 py-2 text-sm font-medium rounded-lg text-blue-500 hover:bg-blue-500/10 transition-all duration-200",

            _ when isActive => "px-4 py-2 text-sm font-medium rounded-lg bg-gray-500 text-white shadow-lg transition-all duration-200",
            _ =>
                "px-4 py-2 text-sm font-medium rounded-lg text-gray-400 hover:bg-gray-500/10 transition-all duration-200"
        };

    private string GetSyntaxHeader()
    {
        var locValue = AppState.Loc("WEBFRONT_TABLE_SYNTAX");
        // Remove redundant "Syntax:" prefix if present
        if (locValue.StartsWith("Syntax:", StringComparison.OrdinalIgnoreCase))
        {
            return locValue.Substring(7).TrimStart();
        }
        return locValue;
    }

    private MarkupString HighlightSearchTerm(string? text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(SearchTerm))
            return new MarkupString(System.Web.HttpUtility.HtmlEncode(text ?? ""));

        var searchTerm = SearchTerm.Trim();
        var textLower = text.ToLowerInvariant();
        var searchLower = searchTerm.ToLowerInvariant();

        if (!textLower.Contains(searchLower))
            return new MarkupString(System.Web.HttpUtility.HtmlEncode(text));

        var encodedText = System.Web.HttpUtility.HtmlEncode(text);
        var highlighted = encodedText.Replace(searchTerm, 
            $"<mark class=\"bg-primary/20 text-primary\">{System.Web.HttpUtility.HtmlEncode(searchTerm)}</mark>",
            StringComparison.OrdinalIgnoreCase);

        return new MarkupString(highlighted);
    }

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
