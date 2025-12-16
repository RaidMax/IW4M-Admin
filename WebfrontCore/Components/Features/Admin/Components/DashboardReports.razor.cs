using Microsoft.AspNetCore.Components;
using WebfrontCore.Controllers.API.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Components;

public partial class DashboardReports
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private List<ServerReportsDto> ReportsList = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            ReportsList = (await Api.GetReportsAsync()).ToList();
        }
        catch
        {
        }
    }
}
