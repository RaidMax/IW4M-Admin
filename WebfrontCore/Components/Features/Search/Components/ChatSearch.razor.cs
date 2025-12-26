using System.Globalization;
using Microsoft.AspNetCore.Components;
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
        LocalSentAfter = Model.SentAfterDateTime ?? DateTime.UtcNow.AddHours(-1);
        LocalSentBefore = Model.SentBeforeDateTime ?? DateTime.UtcNow;
    }

    private void Submit()
    {
        // Update model properties (optional, mainly for query building)
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
