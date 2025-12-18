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

        var url = NavManager.GetUriWithQueryParameters("/find-client", new Dictionary<string, object?>
        {
            { "clientName", ClientName },
            { "isLegacyQuery", true }
        });

        NavManager.NavigateTo(url);
    }
}
