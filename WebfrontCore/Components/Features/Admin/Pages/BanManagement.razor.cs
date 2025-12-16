using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Core.Services;
using WebfrontCore.QueryHelpers.Models;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class BanManagement
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JsInterop { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    private BanInfoRequest Request { get; set; } = new() { Count = 10, Offset = 0 };
    private List<BanInfo> Results { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }
    private ElementReference LoadMoreTrigger;
    private DotNetObjectReference<BanManagement> _objRef;
    private bool _observerSetup;

    protected override async Task OnInitializedAsync()
    {
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Results != null && HasMoreResults && !_observerSetup)
        {
            _objRef = DotNetObjectReference.Create(this);
            await JsInterop.SetupInfiniteScroll(LoadMoreTrigger, _objRef);
            _observerSetup = true;
        }
    }

    private async Task Search()
    {
        Results = null; // Show loading spinner
        Request.Offset = 0;
        var result = await Api.GetBansAsync(Request);
        Results = result.Results.ToList();
        HasMoreResults = Results.Count >= Request.Count;
        _observerSetup = false; // Reset observer so it re-initializes for new list
        StateHasChanged();
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (IsLoading || !HasMoreResults) return;

        IsLoading = true;
        StateHasChanged();

        Request.Offset += Request.Count;
        var result = await Api.GetBansAsync(Request);

        IsLoading = false;

        if (result.Results.Any())
        {
            Results.AddRange(result.Results);
        }

        if (result.RetrievedResultCount < Request.Count)
        {
            HasMoreResults = false;
            await DisposeAsync(); // Cleanup if no more results
        }

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_observerSetup)
        {
            await JsInterop.RemoveInfiniteScroll(LoadMoreTrigger);
            _observerSetup = false;
        }

        _objRef?.Dispose();
    }

    private int _unbanTargetId;
    private string _unbanReason;
    private bool _showUnbanModal;

    private void OpenUnbanModal(int clientId)
    {
        _unbanTargetId = clientId;
        _unbanReason = string.Empty;
        _showUnbanModal = true;
    }

    private void CloseUnbanModal()
    {
        _showUnbanModal = false;
        _unbanTargetId = 0;
        _unbanReason = string.Empty;
    }

    private async Task ConfirmUnban()
    {
        if (string.IsNullOrWhiteSpace(_unbanReason)) return;

        try
        {
            var message = await Api.UnbanClientAsync(_unbanTargetId, _unbanReason);
            await ToastService.ShowSuccessAsync(message);
            CloseUnbanModal();
            await Search(); // Refresh results
        }
        catch (Exception ex)
        {
            await ToastService.ShowErrorAsync(ex.Message);
        }
    }
}
