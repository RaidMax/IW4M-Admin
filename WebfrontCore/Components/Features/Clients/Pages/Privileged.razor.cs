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

    private static string GetHeaderClass(EFClient.Permission permission) => permission switch
    {
        EFClient.Permission.Console => "bg-level-console/25 text-level-console border-level-console/40",
        EFClient.Permission.Owner => "bg-level-owner/25 text-level-owner border-level-owner/40",
        EFClient.Permission.Creator => "bg-level-owner/25 text-level-owner border-level-owner/40",
        EFClient.Permission.SeniorAdmin => "bg-level-senioradmin/25 text-level-senioradmin border-level-senioradmin/40",
        EFClient.Permission.Administrator => "bg-level-administrator/25 text-level-administrator border-level-administrator/40",
        EFClient.Permission.Moderator => "bg-level-moderator/25 text-level-moderator border-level-moderator/40",
        EFClient.Permission.Trusted => "bg-level-trusted/25 text-level-trusted border-level-trusted/40",
        EFClient.Permission.Flagged => "bg-level-flagged/25 text-level-flagged border-level-flagged/40",
        _ => "bg-blue-500/10 text-blue-500 border-blue-500/20"
    };
}
