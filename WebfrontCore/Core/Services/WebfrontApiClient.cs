using SharedLibraryCore.Dtos;
using Data.Models;
using SharedLibraryCore.Helpers;
using WebfrontCore.Controllers.API.Dtos;
using WebfrontCore.Components.Features.Admin.Models;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.QueryHelpers.Models;
using PenaltyInfo = SharedLibraryCore.Dtos.PenaltyInfo;

namespace WebfrontCore.Core.Services;

public class WebfrontApiClient(HttpClient httpClient) : IWebfrontApiClient
{
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<ServerInfo>> GetServersAsync(Reference.Game? game = null)
    {
        var response =
            await httpClient.GetFromJsonAsync<List<ServerInfo>>($"/api/status/servers{(game.HasValue ? $"?game={game}" : "")}");
        return response ?? [];
    }

    public async Task<ServerInfo> GetServerAsync(string id)
    {
        return await httpClient.GetFromJsonAsync<ServerInfo>($"/api/status/servers/{id}");
    }

    public async Task<IW4MAdminInfo> GetStatusAsync(Reference.Game? game = null)
    {
        return await httpClient.GetFromJsonAsync<IW4MAdminInfo>($"/api/status{(game.HasValue ? $"?game={game}" : "")}");
    }

    public async Task<NavigationData> GetNavigationDataAsync()
    {
        // Debug: Get raw JSON to see what's being returned
        var response = await httpClient.GetAsync("/api/navigation");
        var jsonString = await response.Content.ReadAsStringAsync();

        var result = System.Text.Json.JsonSerializer.Deserialize<NavigationData>(jsonString, _jsonOptions);
        return result;
    }

    public Task<PlayerInfo> GetClientProfileAsync(int clientId, SharedLibraryCore.Interfaces.MetaType? metaFilterType = null) =>
        httpClient.GetFromJsonAsync<PlayerInfo>($"/api/client/{clientId}/profile?metaFilterType={metaFilterType}");


    public Task<ScoreboardInfo> GetScoreboardAsync(string serverId) =>
        httpClient.GetFromJsonAsync<ScoreboardInfo>($"/api/status/server/{serverId}/scoreboard");

    public Task<ResourceQueryHelperResult<BanInfo>?> GetBansAsync(BanInfoRequest request)
    {
        // Construct query string manually or use a helper. For simple GET, manual is OK.
        // request has multiple optional args.
        var query = $"?offset={request.Offset}&count={request.Count}";
        if (!string.IsNullOrEmpty(request.ClientName)) query += $"&clientName={System.Net.WebUtility.UrlEncode(request.ClientName)}";
        if (!string.IsNullOrEmpty(request.ClientGuid)) query += $"&clientGuid={System.Net.WebUtility.UrlEncode(request.ClientGuid)}";
        if (!string.IsNullOrEmpty(request.ClientIP)) query += $"&clientIP={System.Net.WebUtility.UrlEncode(request.ClientIP)}";
        if (request.ClientId.HasValue) query += $"&clientId={request.ClientId}";

        return httpClient
            .GetFromJsonAsync<SharedLibraryCore.Helpers.ResourceQueryHelperResult<BanInfo>>(
                $"/api/admin/bans{query}");
    }

    public Task<IList<AuditInfo>> GetAuditLogAsync(
        PaginationRequest request) =>
        httpClient.GetFromJsonAsync<IList<AuditInfo>>(
            $"/api/admin/audit?offset={request.Offset}&count={request.Count}");

    public async Task<List<CommandResponseInfo>> ExecuteConsoleCommandAsync(
        string serverId, string command)
    {
        var response = await httpClient.PostAsJsonAsync("/api/console/execute", new { ServerId = serverId, Command = command });
        // Even 400 returns content we want to display
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
        {
            // Handle real errors?
        }

        return await response.Content.ReadFromJsonAsync<List<CommandResponseInfo>>();
    }

    public Task<IList<PenaltyInfo>> GetPenaltiesAsync(int offset = 0, int count = 30,
        EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true) =>
        httpClient.GetFromJsonAsync<IList<PenaltyInfo>>(
            $"/api/penalty?offset={offset}&count={count}&showOnly={showOnly}&ignoreAutomated={ignoreAutomated}");

    public Task<IEnumerable<ConfigurationFileInfo>> GetConfigurationFilesAsync() =>
        httpClient.GetFromJsonAsync<IEnumerable<ConfigurationFileInfo>>("/api/configuration/files");

    public async Task SaveConfigurationFileAsync(string fileName, string content)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/configuration/files/{fileName}",
            new ConfigurationFileInfo { FileContent = content });
        response.EnsureSuccessStatusCode();
    }

    public Task<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>> GetPrivilegedClientsAsync() =>
        httpClient.GetFromJsonAsync<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>>("/api/client/privileged");

    public Task<FindClientResponse> SearchClientsAsync(FindClientRequest request)
    {
        var query = $"?name={System.Net.WebUtility.UrlEncode(request.Name)}&offset={request.Offset}&count={request.Count}";
        return httpClient.GetFromJsonAsync<FindClientResponse>($"/api/client/find{query}");
    }

    public Task<TopStatsResponse> GetTopPlayersAsync(int count, int offset, string? serverId = null) =>
        httpClient.GetFromJsonAsync<TopStatsResponse>(
            $"/api/stats/top?count={count}&offset={offset}{(serverId != null ? $"&serverId={serverId}" : "")}");

    public Task<Stats.Dtos.AdvancedStatsInfo> GetAdvancedStatsAsync(int clientId, string? serverId = null) =>
        httpClient.GetFromJsonAsync<Stats.Dtos.AdvancedStatsInfo>(
            $"/api/stats/{clientId}/advanced{(serverId != null ? $"?serverId={serverId}" : "")}");

    public Task<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync() =>
        httpClient.GetFromJsonAsync<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>>("/api/admin/alerts");

    public Task DismissAlertAsync(Guid alertId) =>
        httpClient.PostAsync($"/api/admin/alerts/{alertId}/dismiss", null);

    public Task DismissAllAlertsAsync() =>
        httpClient.PostAsync("/api/admin/alerts/dismiss/all", null);

    public Task<IEnumerable<ServerReportsDto>> GetReportsAsync() =>
        httpClient.GetFromJsonAsync<IEnumerable<ServerReportsDto>>("/api/admin/reports");

    public Task<Controllers.API.AboutDto> GetAboutAsync() =>
        httpClient.GetFromJsonAsync<Controllers.API.AboutDto>("/api/information/about");

    public Task<List<Controllers.API.CommandGroupDto>> GetHelpAsync() =>
        httpClient.GetFromJsonAsync<List<Controllers.API.CommandGroupDto>>("/api/information/help");

    public Task<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>> GetClientMetaAsync(int clientId, int count,
        int offset, long? startAt, SharedLibraryCore.Interfaces.MetaType? metaType)
    {
        var query = $"?count={count}&offset={offset}";
        if (startAt.HasValue) query += $"&startAt={startAt}";
        if (metaType.HasValue) query += $"&metaType={metaType}";

        return httpClient.GetFromJsonAsync<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>>(
            $"/api/client/{clientId}/meta{query}");
    }

    public Task<Controllers.API.InteractionResponse> GetInteractionAsync(string interactionName, string? query = null) =>
        httpClient.GetFromJsonAsync<Controllers.API.InteractionResponse>(
            $"/api/interaction/{System.Net.WebUtility.UrlEncode(interactionName)}{query}", _jsonOptions);

    public async Task<string> UnbanClientAsync(int clientId, string reason)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/penalty/unban/{clientId}", new { Reason = reason });

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
            throw new Exception(errorContent?["message"] ?? "Unban failed");
        }

        var successContent = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
        return successContent?["message"] ?? "Client unbanned successfully";
    }

    public async Task<IEnumerable<ClientResourceResponse>> GetClientsAsync(ClientResourceRequest request)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["offset"] = request.Offset.ToString(),
            ["count"] = request.Count.ToString()
        };

        if (!string.IsNullOrEmpty(request.ClientName))
            queryParams["clientName"] = request.ClientName;
        if (request.IsExactClientName)
            queryParams["isExactClientName"] = "true";
        if (!string.IsNullOrEmpty(request.ClientIp))
            queryParams["clientIP"] = request.ClientIp;
        if (request.IsExactClientIp)
            queryParams["isExactClientIP"] = "true";
        if (!string.IsNullOrEmpty(request.ClientGuid))
            queryParams["clientGuid"] = request.ClientGuid;
        if (request.ClientLevel.HasValue)
            queryParams["clientLevel"] = request.ClientLevel.Value.ToString();
        if (request.GameName.HasValue)
            queryParams["gameName"] = request.GameName.Value.ToString();
        if (request.ClientConnected.HasValue)
            queryParams["clientConnected"] = request.ClientConnected.Value.ToString("o");
        if (!string.IsNullOrEmpty(request.SortColumn))
            queryParams["sortColumn"] = request.SortColumn;
        queryParams["direction"] = ((int)request.Direction).ToString();

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={System.Net.WebUtility.UrlEncode(kvp.Value)}"));
        var response =
            await httpClient
                .GetFromJsonAsync<
                    SharedLibraryCore.Helpers.ResourceQueryHelperResult<ClientResourceResponse>>(
                    $"/api/client/search?{queryString}", _jsonOptions);
        return response.Results;
    }

    public Task<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>> GetMessageContextAsync(string serverId, long when) =>
        httpClient.GetFromJsonAsync<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>>(
            $"/api/stats/message/context?serverId={serverId}&when={when}");

    public Task<List<Dictionary<string, string>>> GetAutomatedPenaltyInfoAsync(int penaltyId) =>
        httpClient.GetFromJsonAsync<List<Dictionary<string, string>>>($"/api/stats/penalty/{penaltyId}/context");
}
