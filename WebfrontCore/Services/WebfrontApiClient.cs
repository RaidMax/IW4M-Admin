using System.Collections.Generic;
using System;
using System.Linq;
using SharedLibraryCore.Dtos;
using SharedLibraryCore;
using Data.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WebfrontCore.ViewModels;
using WebfrontCore.Controllers.API.Dtos;

namespace WebfrontCore.Services
{
    public class WebfrontApiClient : IWebfrontApiClient
    {
        private readonly HttpClient _httpClient;
        private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public WebfrontApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<ServerInfo>> GetServersAsync(Reference.Game? game = null)
        {
             var response = await _httpClient.GetFromJsonAsync<List<ServerInfo>>($"/api/status/servers{(game.HasValue ? $"?game={game}" : "")}");
             return response ?? new List<ServerInfo>();
        }

        public async Task<ServerInfo> GetServerAsync(long id)
        {
            return await _httpClient.GetFromJsonAsync<ServerInfo>($"/api/status/servers/{id}");
        }

        public async Task<IW4MAdminInfo> GetStatusAsync(Reference.Game? game = null)
        {
            return await _httpClient.GetFromJsonAsync<IW4MAdminInfo>($"/api/status{(game.HasValue ? $"?game={game}" : "")}");
        }

        public async Task<NavigationData> GetNavigationDataAsync()
        {
             // Debug: Get raw JSON to see what's being returned
             var response = await _httpClient.GetAsync("/api/navigation");
             var jsonString = await response.Content.ReadAsStringAsync();
             
             var result = System.Text.Json.JsonSerializer.Deserialize<NavigationData>(jsonString, _jsonOptions);
             return result;
        }

        public Task<PlayerInfo> GetClientProfileAsync(int clientId, SharedLibraryCore.Interfaces.MetaType? metaFilterType = null) =>
            _httpClient.GetFromJsonAsync<PlayerInfo>($"/api/client/{clientId}/profile?metaFilterType={metaFilterType}");


        public Task<WebfrontCore.ViewModels.ScoreboardInfo> GetScoreboardAsync(long serverId) =>
            _httpClient.GetFromJsonAsync<WebfrontCore.ViewModels.ScoreboardInfo>($"/api/status/server/{serverId}/scoreboard");

        public Task<SharedLibraryCore.Helpers.ResourceQueryHelperResult<WebfrontCore.QueryHelpers.Models.BanInfo>> GetBansAsync(WebfrontCore.QueryHelpers.Models.BanInfoRequest request)
        {
             // Construct query string manually or use a helper. For simple GET, manual is OK.
             // request has multiple optional args.
             var query = $"?offset={request.Offset}&count={request.Count}";
             if (!string.IsNullOrEmpty(request.ClientName)) query += $"&clientName={System.Net.WebUtility.UrlEncode(request.ClientName)}";
             if (!string.IsNullOrEmpty(request.ClientGuid)) query += $"&clientGuid={System.Net.WebUtility.UrlEncode(request.ClientGuid)}";
             if (!string.IsNullOrEmpty(request.ClientIP)) query += $"&clientIP={System.Net.WebUtility.UrlEncode(request.ClientIP)}";
             if (request.ClientId.HasValue) query += $"&clientId={request.ClientId}";
             
             return _httpClient.GetFromJsonAsync<SharedLibraryCore.Helpers.ResourceQueryHelperResult<WebfrontCore.QueryHelpers.Models.BanInfo>>($"/api/admin/bans{query}");
        }

        public Task<System.Collections.Generic.IList<SharedLibraryCore.Dtos.AuditInfo>> GetAuditLogAsync(SharedLibraryCore.Dtos.PaginationRequest request) =>
             _httpClient.GetFromJsonAsync<System.Collections.Generic.IList<SharedLibraryCore.Dtos.AuditInfo>>($"/api/admin/audit?offset={request.Offset}&count={request.Count}");

        public async Task<System.Collections.Generic.List<SharedLibraryCore.Dtos.CommandResponseInfo>> ExecuteConsoleCommandAsync(long serverId, string command)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/console/execute", new { ServerId = serverId, Command = command });
            // Even 400 returns content we want to display
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            {
                 // Handle real errors?
            }
            return await response.Content.ReadFromJsonAsync<System.Collections.Generic.List<SharedLibraryCore.Dtos.CommandResponseInfo>>();
        }

        public Task<System.Collections.Generic.IList<SharedLibraryCore.Dtos.PenaltyInfo>> GetPenaltiesAsync(int offset = 0, int count = 30, Data.Models.EFPenalty.PenaltyType showOnly = Data.Models.EFPenalty.PenaltyType.Any, bool ignoreAutomated = true) =>
            _httpClient.GetFromJsonAsync<System.Collections.Generic.IList<SharedLibraryCore.Dtos.PenaltyInfo>>($"/api/penalty?offset={offset}&count={count}&showOnly={showOnly}&ignoreAutomated={ignoreAutomated}");

        public Task<IEnumerable<WebfrontCore.ViewModels.ConfigurationFileInfo>> GetConfigurationFilesAsync() =>
            _httpClient.GetFromJsonAsync<IEnumerable<WebfrontCore.ViewModels.ConfigurationFileInfo>>("/api/configuration/files");

        public async Task SaveConfigurationFileAsync(string fileName, string content)
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/configuration/files/{fileName}", new WebfrontCore.ViewModels.ConfigurationFileInfo { FileContent = content });
            response.EnsureSuccessStatusCode();
        }

        public Task<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>> GetPrivilegedClientsAsync() =>
            _httpClient.GetFromJsonAsync<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>>("/api/client/privileged");

        public Task<FindClientResponse> SearchClientsAsync(FindClientRequest request)
        {
             var query = $"?name={System.Net.WebUtility.UrlEncode(request.Name)}&offset={request.Offset}&count={request.Count}";
             return _httpClient.GetFromJsonAsync<FindClientResponse>($"/api/client/find{query}");
        }

        public Task<WebfrontCore.Controllers.API.Dtos.TopStatsResponse> GetTopPlayersAsync(int count, int offset, string serverId = null) =>
            _httpClient.GetFromJsonAsync<WebfrontCore.Controllers.API.Dtos.TopStatsResponse>($"/api/stats/top?count={count}&offset={offset}{(serverId != null ? $"&serverId={serverId}" : "")}");

        public Task<Stats.Dtos.AdvancedStatsInfo> GetAdvancedStatsAsync(int clientId, string serverId = null) =>
            _httpClient.GetFromJsonAsync<Stats.Dtos.AdvancedStatsInfo>($"/api/stats/{clientId}/advanced{(serverId != null ? $"?serverId={serverId}" : "")}");

        public Task<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync() =>
            _httpClient.GetFromJsonAsync<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>>("/api/admin/alerts");

        public Task DismissAlertAsync(Guid alertId) =>
            _httpClient.PostAsync($"/api/admin/alerts/{alertId}/dismiss", null);
        
        public Task DismissAllAlertsAsync() =>
            _httpClient.PostAsync("/api/admin/alerts/dismiss/all", null);

        public Task<IEnumerable<WebfrontCore.Controllers.API.Dtos.ServerReportsDto>> GetReportsAsync() =>
            _httpClient.GetFromJsonAsync<IEnumerable<WebfrontCore.Controllers.API.Dtos.ServerReportsDto>>("/api/admin/reports");

        public Task<WebfrontCore.Controllers.API.AboutDto> GetAboutAsync() =>
            _httpClient.GetFromJsonAsync<WebfrontCore.Controllers.API.AboutDto>("/api/information/about");

        public Task<List<WebfrontCore.Controllers.API.CommandGroupDto>> GetHelpAsync() =>
            _httpClient.GetFromJsonAsync<List<WebfrontCore.Controllers.API.CommandGroupDto>>("/api/information/help");

        public Task<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>> GetClientMetaAsync(int clientId, int count, int offset, long? startAt, SharedLibraryCore.Interfaces.MetaType? metaType)
        {
            var query = $"?count={count}&offset={offset}";
            if (startAt.HasValue) query += $"&startAt={startAt}";
            if (metaType.HasValue) query += $"&metaType={metaType}";
            
            return _httpClient.GetFromJsonAsync<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>>($"/api/client/{clientId}/meta{query}");
        }

        public Task<WebfrontCore.Controllers.API.InteractionResponse> GetInteractionAsync(string interactionName) =>
            _httpClient.GetFromJsonAsync<WebfrontCore.Controllers.API.InteractionResponse>($"/api/interaction/{System.Net.WebUtility.UrlEncode(interactionName)}", _jsonOptions);

        public async Task<string> UnbanClientAsync(int clientId, string reason)
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/penalty/unban/{clientId}", new { Reason = reason });
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
                throw new Exception(errorContent?["message"] ?? "Unban failed");
            }
            
            var successContent = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
            return successContent?["message"] ?? "Client unbanned successfully";
        }

        public async Task<IEnumerable<WebfrontCore.QueryHelpers.Models.ClientResourceResponse>> GetClientsAsync(WebfrontCore.QueryHelpers.Models.ClientResourceRequest request)
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
            queryParams["direction"] = ((int)request.Direction).ToString();

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={System.Net.WebUtility.UrlEncode(kvp.Value)}"));
            var response = await _httpClient.GetFromJsonAsync<SharedLibraryCore.Helpers.ResourceQueryHelperResult<WebfrontCore.QueryHelpers.Models.ClientResourceResponse>>($"/api/client/search?{queryString}", _jsonOptions);
            return response.Results;
        }

        public Task<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>> GetMessageContextAsync(string serverId, long when) =>
            _httpClient.GetFromJsonAsync<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>>($"/api/stats/message/context?serverId={serverId}&when={when}");

        public Task<List<Dictionary<string, string>>> GetAutomatedPenaltyInfoAsync(int penaltyId) =>
            _httpClient.GetFromJsonAsync<List<Dictionary<string, string>>>($"/api/stats/penalty/{penaltyId}/context");
    }
}
