using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class AuditLog
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JS { get; set; }
    private PaginationRequest Request { get; } = new() { Count = 50, Offset = 0 };
    private List<AuditInfo> Results { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }
    private ElementReference loadMoreTrigger;
    private DotNetObjectReference<AuditLog> objRef;
    private bool observerSetUp = false;
    private string _error;

    private static string DataDetailsPolicy => $"Permissions.{WebfrontEntity.AuditLogDataDetails}.{WebfrontPermission.Read}";

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Set up observer after first data load, not on firstRender
        if (!observerSetUp && HasMoreResults && Results != null)
        {
            observerSetUp = true;
            objRef = DotNetObjectReference.Create(this);
            await JS.SetupInfiniteScroll(loadMoreTrigger, objRef);
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
        if (objRef != null)
        {
            await JS.RemoveInfiniteScroll(loadMoreTrigger);
            objRef.Dispose();
        }
    }
}
