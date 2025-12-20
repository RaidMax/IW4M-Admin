using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class AuditLog
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    
    private PaginationRequest Request { get; } = new() { Count = 50, Offset = 0 };
    private List<AuditInfo> Results { get; set; }
    private bool HasMoreResults { get; set; } = true;
    private bool IsLoading { get; set; }
    private string _error;

    private static string DataDetailsPolicy => $"Permissions.{WebfrontEntity.AuditLogDataDetails}.{WebfrontPermission.Read}";

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
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

    private async Task LoadMore()
    {
        if (!HasMoreResults || IsLoading) return;
        Request.Offset += Request.Count;
        await LoadData();
        StateHasChanged();
    }
}
