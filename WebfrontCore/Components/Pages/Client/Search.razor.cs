using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Pages.Client;

public partial class Search
{
    [SupplyParameterFromQuery] public string clientName { get; set; }
    [SupplyParameterFromQuery] public string q { get; set; }

    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required SharedLibraryCore.Configuration.ApplicationConfiguration Config { get; set; }

    public string SearchTerm => clientName ?? q;
    private IEnumerable<FindClientResult> Results;
    private long ResultCount;
    private bool IsLoading = false;

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Results = new List<FindClientResult>();
            return;
        }

        IsLoading = true;
        try
        {
            var request = new FindClientRequest
            {
                Name = SearchTerm,
                Count = 50 // Default
            };
            var response = await Api.SearchClientsAsync(request);
            Results = response.Clients;
            ResultCount = response.TotalFoundClients;
        }
        catch (Exception)
        {
            // Handle error
        }
        finally
        {
            IsLoading = false;
        }
    }
}
