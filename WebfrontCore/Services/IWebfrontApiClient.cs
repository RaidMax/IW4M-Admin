using System.Collections.Generic;
using System.Threading.Tasks;
using WebfrontCore.ViewModels;
using SharedLibraryCore.Dtos;
using SharedLibraryCore;
using Data.Models;
using WebfrontCore.Controllers.API.Dtos;

namespace WebfrontCore.Services
{
    public interface IWebfrontApiClient
    {
        Task<List<ServerInfo>> GetServersAsync(Reference.Game? game = null);
        Task<ServerInfo> GetServerAsync(long id);
        Task<IW4MAdminInfo> GetStatusAsync(Reference.Game? game = null);
        Task<NavigationData> GetNavigationDataAsync();
        Task<PlayerInfo> GetClientProfileAsync(int clientId, SharedLibraryCore.Interfaces.MetaType? metaFilterType = null);
        Task<WebfrontCore.ViewModels.ScoreboardInfo> GetScoreboardAsync(long serverId);
        Task<SharedLibraryCore.Helpers.ResourceQueryHelperResult<WebfrontCore.QueryHelpers.Models.BanInfo>> GetBansAsync(WebfrontCore.QueryHelpers.Models.BanInfoRequest request);
        Task<System.Collections.Generic.IList<SharedLibraryCore.Dtos.AuditInfo>> GetAuditLogAsync(SharedLibraryCore.Dtos.PaginationRequest request);
        Task<System.Collections.Generic.List<SharedLibraryCore.Dtos.CommandResponseInfo>> ExecuteConsoleCommandAsync(long serverId, string command);
        Task<System.Collections.Generic.IList<SharedLibraryCore.Dtos.PenaltyInfo>> GetPenaltiesAsync(int offset = 0, int count = 30, Data.Models.EFPenalty.PenaltyType showOnly = Data.Models.EFPenalty.PenaltyType.Any, bool ignoreAutomated = true);
        Task<IEnumerable<WebfrontCore.ViewModels.ConfigurationFileInfo>> GetConfigurationFilesAsync();
        Task SaveConfigurationFileAsync(string fileName, string content);
        Task<System.Collections.Generic.Dictionary<Data.Models.Client.EFClient.Permission, System.Collections.Generic.IList<SharedLibraryCore.Dtos.ClientInfo>>> GetPrivilegedClientsAsync();
        Task<FindClientResponse> SearchClientsAsync(SharedLibraryCore.Dtos.FindClientRequest request);
        Task<WebfrontCore.Controllers.API.Dtos.TopStatsResponse> GetTopPlayersAsync(int count, int offset, string serverId = null);
        Task<Stats.Dtos.AdvancedStatsInfo> GetAdvancedStatsAsync(int clientId, string serverId = null);
        Task<System.Collections.Generic.IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync();
        Task DismissAlertAsync(System.Guid alertId);
        Task DismissAllAlertsAsync();
        Task<System.Collections.Generic.IEnumerable<WebfrontCore.Controllers.API.Dtos.ServerReportsDto>> GetReportsAsync();
        Task<WebfrontCore.Controllers.API.AboutDto> GetAboutAsync();
        Task<System.Collections.Generic.List<WebfrontCore.Controllers.API.CommandGroupDto>> GetHelpAsync();
        Task<System.Collections.Generic.IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>> GetClientMetaAsync(int clientId, int count, int offset, long? startAt, SharedLibraryCore.Interfaces.MetaType? metaType);
        Task<WebfrontCore.Controllers.API.InteractionResponse> GetInteractionAsync(string interactionName);
        Task<string> UnbanClientAsync(int clientId, string reason);
        Task<IEnumerable<WebfrontCore.QueryHelpers.Models.ClientResourceResponse>> GetClientsAsync(WebfrontCore.QueryHelpers.Models.ClientResourceRequest request);
        Task<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>> GetMessageContextAsync(string serverId, long when);
        Task<List<Dictionary<string, string>>> GetAutomatedPenaltyInfoAsync(int penaltyId);
    }

    public class NavigationData
    {
        [System.Text.Json.Serialization.JsonPropertyName("user")]
        public ClientInfo User { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("authorized")]
        public bool Authorized { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("localization")]
        public Dictionary<string, string> Localization { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pages")]
        public IEnumerable<Page> Pages { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("interactions")]
        public IEnumerable<InteractionData> Interactions { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("communityInformation")]
        public CommunityInformation CommunityInformation { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("totalClientCount")]
        public int TotalClientCount { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("totalAdminCount")]
        public int TotalAdminCount { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("totalReportCount")]
        public int TotalReportCount { get; set; }
    }

    public class Page
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("location")]
        public string Location { get; set; }
    }

    public interface IInteractionData
    {
        string InteractionId { get; set; }
        int MinimumPermission { get; set; }
        string Name { get; set; }
        string DisplayMeta { get; set; }
    }

    public class InteractionData : IInteractionData
    {
        public string InteractionId { get; set; }
        public int MinimumPermission { get; set; }
        public string Name { get; set; }
        public string DisplayMeta { get; set; }
    }

    public class CommunityInformation
    {
        public bool IsEnabled { get; set; }
        public SocialAccountConfiguration[] SocialAccounts { get; set; }
    }

    public class SocialAccountConfiguration
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string IconId { get; set; }
        public string IconUrl { get; set; }
    }
}
