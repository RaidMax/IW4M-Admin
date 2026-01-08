using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class BanManagement
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }

    private BanInfoRequest Request { get; set; } = new();
    private List<BanInfo> Results { get; set; } = [];
    private bool HasSearched { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }
    private long TotalCount { get; set; }
    private string? ValidationError { get; set; }

    private DotNetObjectReference<BanManagement>? _dotNetRef;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
        }

        return Task.CompletedTask;
    }

    private async Task Search()
    {
        HasSearched = true;
        Results.Clear();
        Request.Offset = 0;
        TotalCount = 0;
        HasMoreResults = true;
        ValidationError = null;
        StateHasChanged();

        // Validate minimum name length if name search is provided
        if (!string.IsNullOrWhiteSpace(Request.ClientName) && Request.ClientName.Length < AppConfig.MinimumNameLength)
        {
            ValidationError = AppState.Loc("WEBFRONT_SEARCH_LENGTH_ERROR").FormatExt(AppConfig.MinimumNameLength);
            HasMoreResults = false;
            StateHasChanged();
            return;
        }

        var result = await DataService.GetBansAsync(Request);
        Results = result?.Results?.ToList() ?? [];
        TotalCount = result?.TotalResultCount ?? 0;
        HasMoreResults = Results.Count >= Request.Count && Results.Count < TotalCount;
        StateHasChanged();

        // Initialize infinite scroll after first results are loaded
        if (_dotNetRef != null && HasMoreResults)
        {
            try
            {
                await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _dotNetRef, "loadMoreBansTrigger");
            }
            catch (InvalidOperationException)
            {
                // JS interop not available - safe to ignore
            }
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (IsLoading || !HasMoreResults)
            return;

        IsLoading = true;
        StateHasChanged();

        Request.Offset += Request.Count;
        var result = await DataService.GetBansAsync(Request);

        IsLoading = false;

        if (result?.Results?.Any() == true)
        {
            Results.AddRange(result.Results);
        }

        if (result?.RetrievedResultCount < Request.Count || Results.Count >= TotalCount)
        {
            HasMoreResults = false;
            // Disconnect the observer when no more results
            try
            {
                await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
            }
            catch (InvalidOperationException)
            {
                // JS interop not available - safe to ignore
            }
        }

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            // JS interop not available during static rendering - safe to ignore
        }

        _dotNetRef?.Dispose();
    }

    private int _unbanTargetId;
    private string _unbanReason = string.Empty;
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
        if (string.IsNullOrWhiteSpace(_unbanReason))
            return;

        try
        {
            var message = await DataService.UnbanClientAsync(_unbanTargetId, _unbanReason);
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
