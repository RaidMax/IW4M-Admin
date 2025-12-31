using System.Globalization;
using System.Web;
using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Search.Components;

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

    protected override void OnInitialized()
    {
        // Parse query params from current URL to pre-populate form
        var uri = new Uri(NavManager.Uri);
        var query = HttpUtility.ParseQueryString(uri.Query);
        
        if (!string.IsNullOrEmpty(query["clientName"]))
            Model.ClientName = query["clientName"];
        if (bool.TryParse(query["isExactClientName"], out var exactName))
            Model.IsExactClientName = exactName;
        if (!string.IsNullOrEmpty(query["clientIP"]))
            Model.ClientIp = query["clientIP"];
        if (bool.TryParse(query["isExactClientIP"], out var exactIp))
            Model.IsExactClientIp = exactIp;
        if (!string.IsNullOrEmpty(query["clientGuid"]))
            Model.ClientGuid = query["clientGuid"];
        if (Enum.TryParse<EFClient.Permission>(query["clientLevel"], out var level))
            Model.ClientLevel = level;
        if (Enum.TryParse<Reference.Game>(query["gameName"], out var game))
            Model.GameName = game;
        if (DateTime.TryParse(query["clientConnected"], out var connected))
            Model.ClientConnected = connected;
        if (int.TryParse(query["direction"], out var dir))
            Model.Direction = (SortDirection)dir;
    }

    private string? _validationError;
    [Inject] public required SharedLibraryCore.Configuration.ApplicationConfiguration AppConfig { get; set; }

    private void Submit()
    {
        _validationError = null;

        if (!string.IsNullOrWhiteSpace(Model.ClientName) && Model.ClientName.Length < AppConfig.MinimumNameLength)
        {
            _validationError = AppState.Loc("WEBFRONT_SEARCH_LENGTH_ERROR").FormatExt(AppConfig.MinimumNameLength);
            StateHasChanged();
            return;
        }

        // Close the modal
        AppState.IsAdvancedSearchOpen = false;
        
        // Build query string
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

        var url = NavManager.GetUriWithQueryParameters("/find-client", query);
        NavManager.NavigateTo(url);
    }
}
