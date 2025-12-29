using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Pages;

public partial class Privileged
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ApplicationConfiguration Config { get; set; }

    [SupplyParameterFromQuery] public string? Game { get; set; }

    private Reference.Game? SelectedGame { get; set; }
    private IEnumerable<Reference.Game> ActiveGames { get; set; } = [];
    private Dictionary<EFClient.Permission, IList<ClientInfo>>? AllPrivilegedClients;
    private Dictionary<EFClient.Permission, IList<ClientInfo>>? FilteredPrivilegedClients;

    protected override async Task OnParametersSetAsync()
    {
        // Parse game filter from query string
        if (!string.IsNullOrEmpty(Game) && Enum.TryParse<Reference.Game>(Game, out var g))
        {
            SelectedGame = g;
        }
        else
        {
            SelectedGame = null;
        }

        // Fetch data if not already loaded
        if (AllPrivilegedClients is null)
        {
            try
            {
                AllPrivilegedClients = await DataService.GetPrivilegedClientsAsync();
                ActiveGames = AllPrivilegedClients.Values
                    .SelectMany(clients => clients)
                    .Select(c => c.Game)
                    .Distinct()
                    .OrderBy(g => g.ToString());
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                NavManager.NavigateTo("/");
                return;
            }
            catch (Exception)
            {
                return;
            }
        }

        // Apply filter
        FilteredPrivilegedClients = ApplyGameFilter(AllPrivilegedClients, SelectedGame);
    }

    private static Dictionary<EFClient.Permission, IList<ClientInfo>>? ApplyGameFilter(
        Dictionary<EFClient.Permission, IList<ClientInfo>>? clients,
        Reference.Game? game)
    {
        if (clients is null) return null;
        if (!game.HasValue) return clients;

        return clients
            .Select(kvp => new
            {
                kvp.Key,
                Value = kvp.Value.Where(c => c.Game == game.Value).ToList()
            })
            .Where(x => x.Value.Count > 0)
            .ToDictionary(x => x.Key, x => (IList<ClientInfo>)x.Value);
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
