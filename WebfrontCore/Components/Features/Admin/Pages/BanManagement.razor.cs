using Microsoft.AspNetCore.Components;
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
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }

    private BanInfoRequest Request { get; set; } = new();
    private List<BanInfo> Results { get; set; } = [];
    private bool HasSearched { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }
    private long TotalCount { get; set; }
    private string? ValidationError { get; set; }

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
    }

    private async Task LoadMore()
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
        }

        StateHasChanged();
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
