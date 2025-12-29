using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Configuration;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class BanManagement
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    
    // JS Interop removed as we are using a native Load More button now.
    
    private BanInfoRequest Request { get; set; } = new() { Count = 10, Offset = 0 };
    private List<BanInfo> Results { get; set; }
    private bool HasSearched { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }

    protected override async Task OnInitializedAsync()
    {
    }

    private async Task Search()
    {
        HasSearched = true;
        Results = null; // Show loading spinner
        Request.Offset = 0;
        var result = await DataService.GetBansAsync(Request);
        Results = result.Results.ToList();
        HasMoreResults = Results.Count >= Request.Count;
        StateHasChanged();
    }

    private async Task LoadMore()
    {
        if (IsLoading || !HasMoreResults) return;

        IsLoading = true;
        StateHasChanged();

        Request.Offset += Request.Count;
        var result = await DataService.GetBansAsync(Request);

        IsLoading = false;

        if (result.Results.Any())
        {
            Results.AddRange(result.Results);
        }

        if (result.RetrievedResultCount < Request.Count)
        {
            HasMoreResults = false;
        }

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        // No more JS references to dispose
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
