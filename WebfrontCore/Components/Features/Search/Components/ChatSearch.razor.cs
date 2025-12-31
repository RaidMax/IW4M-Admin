using System.Globalization;
using System.Web;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Search.Components;

public partial class ChatSearch
{
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Parameter] public ChatResourceRequest Model { get; set; } = new();
    private DateTime LocalSentAfter { get; set; }
    private DateTime LocalSentBefore { get; set; }
    private bool _oldestFirst { get; set; }

    protected override void OnInitialized()
    {
        // Parse query params from current URL to pre-populate form
        var uri = new Uri(NavManager.Uri);
        var query = HttpUtility.ParseQueryString(uri.Query);

        if (!string.IsNullOrEmpty(query["messageContains"]))
            Model.MessageContains = query["messageContains"];
        if (bool.TryParse(query["isExactMatch"], out var exact))
            Model.IsExactMatch = exact;
        if (int.TryParse(query["clientId"], out var clientId))
            Model.ClientId = clientId;
        if (!string.IsNullOrEmpty(query["serverId"]))
            Model.ServerId = query["serverId"];
        if (DateTime.TryParse(query["sentAfter"], out var after))
            Model.SentAfter = after;
        if (!string.IsNullOrEmpty(query["sentAfterTime"]))
            Model.SentAfterTime = query["sentAfterTime"];
        if (DateTime.TryParse(query["sentBefore"], out var before))
            Model.SentBefore = before;
        if (!string.IsNullOrEmpty(query["sentBeforeTime"]))
            Model.SentBeforeTime = query["sentBeforeTime"];
        if (int.TryParse(query["direction"], out var dir))
            _oldestFirst = dir == (int)SortDirection.Ascending;

        LocalSentAfter = Model.SentAfterDateTime ?? DateTime.UtcNow.AddHours(-1);
        LocalSentBefore = Model.SentBeforeDateTime ?? DateTime.UtcNow;
    }

    private string? _validationError;
    [Inject] public required SharedLibraryCore.Configuration.ApplicationConfiguration AppConfig { get; set; }

    private void Submit()
    {
        _validationError = null;

        if (!string.IsNullOrWhiteSpace(Model.MessageContains) && Model.MessageContains.Length < AppConfig.MinimumNameLength)
        {
            _validationError = AppState.Loc("WEBFRONT_SEARCH_LENGTH_ERROR").FormatExt(AppConfig.MinimumNameLength);
            StateHasChanged();
            return;
        }

        // Close the modal
        AppState.IsAdvancedSearchOpen = false;

        // Update model properties
        Model.SentAfter = LocalSentAfter.Date;
        Model.SentAfterTime = LocalSentAfter.ToString("HH:mm");
        Model.SentBefore = LocalSentBefore.Date;
        Model.SentBeforeTime = LocalSentBefore.ToString("HH:mm");

        var query = new Dictionary<string, object?>
        {
            { "messageContains", Model.MessageContains },
            { "isExactMatch", Model.IsExactMatch },
            { "clientId", Model.ClientId },
            { "serverId", Model.ServerId },
            { "sentAfter", Model.SentAfter?.ToString("s", CultureInfo.InvariantCulture) },
            { "sentAfterTime", Model.SentAfterTime },
            { "sentBefore", Model.SentBefore.ToString("s", CultureInfo.InvariantCulture) },
            { "sentBeforeTime", Model.SentBeforeTime },
            { "direction", _oldestFirst ? (int)SortDirection.Ascending : (int)SortDirection.Descending }
        };

        var url = NavManager.GetUriWithQueryParameters("find-message", query);
        NavManager.NavigateTo(url, forceLoad: true);
    }
}
