using System.Globalization;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.QueryHelpers.Models;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Shared.Partials.Search;

public partial class ClientSearch
{
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public ClientResourceRequest Model { get; set; } = new();

    private bool IsAscending
    {
        get => Model.Direction == SortDirection.Ascending;
        set => Model.Direction = value ? SortDirection.Ascending : SortDirection.Descending;
    }

    private void Submit()
    {
        // Build query string manually or use helper
        var query = new Dictionary<string, object?>
        {
            { "clientName", Model.ClientName },
            { "isExactClientName", Model.IsExactClientName },
            { "clientIP", Model.ClientIp },
            { "isExactClientIP", Model.IsExactClientIp },
            { "clientGuid", Model.ClientGuid },
            { "clientLevel", Model.ClientLevel?.ToString() },
            { "gameName", Model.GameName?.ToString() },
            { "clientConnected", Model.ClientConnected?.ToString("s", CultureInfo.InvariantCulture) },
            { "direction", ((int)Model.Direction).ToString() }
        };

        var url = NavManager.GetUriWithQueryParameters("/Client/AdvancedFind", query);
        NavManager.NavigateTo(url);
    }
}
