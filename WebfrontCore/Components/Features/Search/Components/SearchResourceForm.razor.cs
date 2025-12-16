using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Search.Components;

public partial class SearchResourceForm
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    
    private string ClientName { get; set; }
    private string SearchType { get; set; } = "client";

    private void BasicSubmit()
    {
        if (string.IsNullOrWhiteSpace(ClientName)) return;

        // Basic search goes to Client/AdvancedFind with just clientName
        var url = NavManager.GetUriWithQueryParameters("/Client/AdvancedFind", new Dictionary<string, object?>
        {
            { "clientName", ClientName },
            { "isLegacyQuery", true }
        });

        NavManager.NavigateTo(url);
    }
}
