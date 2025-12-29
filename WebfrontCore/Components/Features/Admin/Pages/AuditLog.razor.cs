using Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class AuditLog : IAsyncDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }

    // Filter state
    private string _searchQuery = "";
    private List<EFChangeHistory.ChangeType> _selectedActionTypes = [];
    private int? _originId;
    private int? _targetId;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    // Results and statistics
    private List<AuditInfo> Results { get; set; } = [];
    private AuditStatistics? Statistics { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool _isLoading;
    private string? _error;
    private DotNetObjectReference<AuditLog>? _dotNetRef;

    // Pagination
    private int _offset;
    private const int PageSize = 50;

    // View mode
    private bool _groupByAction;
    
    // Collapsed group state
    private HashSet<string> _collapsedGroups = [];

    private static string DataDetailsPolicy => $"Permissions.{WebfrontEntity.AuditLogDataDetails}.{WebfrontPermission.Read}";

    // Available action types (excluding Ban which is filtered out at repository level)
    private static readonly EFChangeHistory.ChangeType[] AvailableActionTypes =
    [
        EFChangeHistory.ChangeType.Permission,
        EFChangeHistory.ChangeType.Command
    ];

    private string GetBadgeClass(string action)
    {
        var upperAction = action?.ToUpperInvariant() ?? "";

        if (upperAction.Contains("BAN") && !upperAction.Contains("UNBAN"))
            return "bg-red-900/30 text-red-400 border-red-900/50";
        if (upperAction.Contains("KICK")) return "bg-orange-900/30 text-orange-400 border-orange-900/50";
        if (upperAction.Contains("FLAG") && !upperAction.Contains("UNFLAG"))
            return "bg-yellow-900/30 text-yellow-400 border-yellow-900/50";
        if (upperAction.Contains("WARN")) return "bg-yellow-900/30 text-yellow-400 border-yellow-900/50";

        if (upperAction.Contains("UNBAN") || upperAction.Contains("UNFLAG"))
            return "bg-green-900/30 text-green-400 border-green-900/50";

        if (upperAction.Contains("COMMAND") || upperAction.Contains("LOGIN"))
            return "bg-blue-900/30 text-blue-400 border-blue-900/50";

        return "bg-slate-700/30 text-slate-400 border-slate-700/50";
    }

    protected override void OnInitialized()
    {
        ParseQueryParameters();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
        await LoadStatistics();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _dotNetRef, "loadMoreAuditTrigger");
        }
    }

    private void ParseQueryParameters()
    {
        var uri = new Uri(Navigation.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("search", out var search))
            _searchQuery = search.ToString();

        if (query.TryGetValue("action", out var actions))
        {
            _selectedActionTypes = actions.ToString().Split(',')
                .Where(a => Enum.TryParse<EFChangeHistory.ChangeType>(a, out _))
                .Select(a => Enum.Parse<EFChangeHistory.ChangeType>(a))
                .ToList();
        }

        if (query.TryGetValue("admin", out var admin) && int.TryParse(admin, out var adminId))
            _originId = adminId;

        if (query.TryGetValue("target", out var target) && int.TryParse(target, out var targetId))
            _targetId = targetId;

        if (query.TryGetValue("from", out var from) && DateTime.TryParse(from, out var dateFrom))
            _dateFrom = dateFrom;

        if (query.TryGetValue("to", out var to) && DateTime.TryParse(to, out var dateTo))
            _dateTo = dateTo;

        if (query.TryGetValue("group", out var group))
            _groupByAction = group == "true";
    }

    private void UpdateUrl()
    {
        var queryParams = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(_searchQuery))
            queryParams["search"] = _searchQuery;

        if (_selectedActionTypes.Count > 0)
            queryParams["action"] = string.Join(",", _selectedActionTypes);

        if (_originId.HasValue)
            queryParams["admin"] = _originId.Value.ToString();

        if (_targetId.HasValue)
            queryParams["target"] = _targetId.Value.ToString();

        if (_dateFrom.HasValue)
            queryParams["from"] = _dateFrom.Value.ToString("yyyy-MM-dd");

        if (_dateTo.HasValue)
            queryParams["to"] = _dateTo.Value.ToString("yyyy-MM-dd");

        if (_groupByAction)
            queryParams["group"] = "true";

        var newUri = QueryHelpers.AddQueryString(Navigation.Uri.Split('?')[0], queryParams!);
        Navigation.NavigateTo(newUri, replace: true);
    }

    private AuditFilterRequest BuildRequest()
    {
        return new AuditFilterRequest
        {
            Count = PageSize,
            Offset = _offset,
            SearchQuery = string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery,
            ActionTypes = _selectedActionTypes.Count > 0 ? _selectedActionTypes : [],
            OriginId = _originId,
            TargetId = _targetId,
            After = _dateFrom,
            Before = _dateTo?.AddDays(1) // Include the entire end date
        };
    }

    private async Task LoadData()
    {
        if (_isLoading) return;
        _isLoading = true;
        StateHasChanged();

        try
        {
            var request = BuildRequest();
            var result = await DataService.GetAuditLogAsync(request);
            
            if (result.Any())
            {
                Results.AddRange(result);
                HasMoreResults = result.Count >= PageSize;
            }
            else
            {
                HasMoreResults = false;
            }
            _error = null;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading audit log: {ex.Message}");
            _error = "Failed to load audit log. You may not have permission to view this page.";
            HasMoreResults = false;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadStatistics()
    {
        try
        {
            var request = BuildRequest();
            request.Offset = 0; // Statistics don't need pagination
            Statistics = await DataService.GetAuditStatisticsAsync(request);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading statistics: {ex.Message}");
        }
    }

    private async Task ApplyFilters()
    {
        _offset = 0;
        Results.Clear();
        HasMoreResults = true;
        UpdateUrl();
        StateHasChanged();
        await LoadData();
        await LoadStatistics();
    }

    private async Task ClearFilters()
    {
        _searchQuery = "";
        _selectedActionTypes = [];
        _originId = null;
        _targetId = null;
        _dateFrom = null;
        _dateTo = null;
        _groupByAction = false;
        _offset = 0;
        Results.Clear();
        HasMoreResults = true;
        UpdateUrl();
        StateHasChanged();
        await LoadData();
        await LoadStatistics();
    }

    private void ToggleActionType(EFChangeHistory.ChangeType actionType)
    {
        if (_selectedActionTypes.Contains(actionType))
            _selectedActionTypes.Remove(actionType);
        else
            _selectedActionTypes.Add(actionType);
    }

    private void ToggleGroupCollapsed(string groupKey)
    {
        if (!_collapsedGroups.Add(groupKey))
            _collapsedGroups.Remove(groupKey);
    }

    private bool IsGroupCollapsed(string groupKey) => _collapsedGroups.Contains(groupKey);

    private async Task HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await ApplyFilters();
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (!HasMoreResults || _isLoading) return;
        _offset += PageSize;
        await LoadData();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef is not null)
        {
            await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
            _dotNetRef.Dispose();
        }
    }
}
