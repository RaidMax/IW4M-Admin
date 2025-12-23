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
        EFClient.Permission.Console => "bg-level-console/40 text-level-console border-level-console/60",
        EFClient.Permission.Owner => "bg-level-owner/40 text-level-owner border-level-owner/60",
        EFClient.Permission.Creator => "bg-level-owner/40 text-level-owner border-level-owner/60",
        EFClient.Permission.SeniorAdmin => "bg-level-senioradmin/40 text-level-senioradmin border-level-senioradmin/60",
        EFClient.Permission.Administrator => "bg-level-administrator/40 text-level-administrator border-level-administrator/60",
        EFClient.Permission.Moderator => "bg-level-moderator/40 text-level-moderator border-level-moderator/60",
        EFClient.Permission.Trusted => "bg-level-trusted/40 text-level-trusted border-level-trusted/60",
        EFClient.Permission.Flagged => "bg-level-flagged/40 text-level-flagged border-level-flagged/60",
        _ => "bg-blue-500/10 text-blue-500 border-blue-500/20"
    };
}
