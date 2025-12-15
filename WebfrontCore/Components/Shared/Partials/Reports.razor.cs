using Microsoft.AspNetCore.Components;
using WebfrontCore.Controllers.API.Dtos;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Shared.Partials;

public partial class Reports
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
