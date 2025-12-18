using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Components;

public partial class DashboardReports
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private List<ServerReportsInfo> ReportsList = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadReports();
    }

    private async Task LoadReports()
    {
        try
        {
            ReportsList = (await DataService.GetReportsAsync()).ToList();
        }
        catch (Exception ex)
        {
            // Logging is handled in the razor component or valid to ignore here if UI handles empty state
            ReportsList = [];
        }
    }
}
