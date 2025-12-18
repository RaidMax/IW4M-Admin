using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Pages;

public partial class Privileged
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required SharedLibraryCore.Configuration.ApplicationConfiguration Config { get; set; }

    private Dictionary<EFClient.Permission, IList<ClientInfo>> PrivilegedClients;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            PrivilegedClients = await DataService.GetPrivilegedClientsAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // If privacy enabled and not logged in, user might be forbidden
            // However, the menu link should probably be hidden or we redirect?
            // MVC redirects to Index.
            NavManager.NavigateTo("/");
        }
        catch (Exception)
        {
            // Handle error
        }
    }
}
