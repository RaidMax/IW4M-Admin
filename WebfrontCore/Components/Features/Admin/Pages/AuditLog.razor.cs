using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class AuditLog : IAsyncDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    
    private PaginationRequest Request { get; } = new() { Count = 50, Offset = 0 };
    private List<AuditInfo> Results { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }
    private string _error;
    private DotNetObjectReference<AuditLog> _objRef;

    private static string DataDetailsPolicy => $"Permissions.{WebfrontEntity.AuditLogDataDetails}.{WebfrontPermission.Read}";

    private string GetBadgeClass(string action)
    {
        var upperAction = action?.ToUpperInvariant() ?? "";
        
        if (upperAction.Contains("BAN") && !upperAction.Contains("UNBAN")) return "bg-red-900/30 text-red-400 border-red-900/50";
        if (upperAction.Contains("KICK")) return "bg-orange-900/30 text-orange-400 border-orange-900/50";
        if (upperAction.Contains("FLAG") && !upperAction.Contains("UNFLAG")) return "bg-yellow-900/30 text-yellow-400 border-yellow-900/50";
        if (upperAction.Contains("WARN")) return "bg-yellow-900/30 text-yellow-400 border-yellow-900/50";
        
        if (upperAction.Contains("UNBAN") || upperAction.Contains("UNFLAG")) return "bg-green-900/30 text-green-400 border-green-900/50";
        
        if (upperAction.Contains("COMMAND") || upperAction.Contains("LOGIN")) return "bg-blue-900/30 text-blue-400 border-blue-900/50";
        
        return "bg-slate-700/30 text-slate-400 border-slate-700/50";
    }



    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private bool _isObserverInitialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Results != null && !_isObserverInitialized)
        {
            _objRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _objRef, "loadMoreAuditTrigger");
            _isObserverInitialized = true;
        }
    }

    private async Task LoadData()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var result = await DataService.GetAuditLogAsync(Request);
            if (Request.Offset == 0)
            {
                Results = result.ToList();
            }
            else
            {
                Results.AddRange(result);
            }

            HasMoreResults = result.Count >= Request.Count;
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
            IsLoading = false;
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (!HasMoreResults || IsLoading) return;
        Request.Offset += Request.Count;
        await LoadData();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_objRef is not null)
        {
             await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
            _objRef.Dispose();
        }
    }
}
