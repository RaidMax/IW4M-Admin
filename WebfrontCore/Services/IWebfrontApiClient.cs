using SharedLibraryCore.Dtos;
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
        Task<ViewModels.ScoreboardInfo> GetScoreboardAsync(long serverId);

        Task<SharedLibraryCore.Helpers.ResourceQueryHelperResult<QueryHelpers.Models.BanInfo>> GetBansAsync(
            QueryHelpers.Models.BanInfoRequest request);

        Task<IList<AuditInfo>> GetAuditLogAsync(PaginationRequest request);
        Task<List<CommandResponseInfo>> ExecuteConsoleCommandAsync(long serverId, string command);

        Task<IList<PenaltyInfo>> GetPenaltiesAsync(int offset = 0, int count = 30,
            EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true);

        Task<IEnumerable<ViewModels.ConfigurationFileInfo>> GetConfigurationFilesAsync();
        Task SaveConfigurationFileAsync(string fileName, string content);
        Task<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>> GetPrivilegedClientsAsync();
        Task<FindClientResponse> SearchClientsAsync(FindClientRequest request);
        Task<TopStatsResponse> GetTopPlayersAsync(int count, int offset, string? serverId = null);
        Task<Stats.Dtos.AdvancedStatsInfo> GetAdvancedStatsAsync(int clientId, string? serverId = null);
        Task<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync();
        Task DismissAlertAsync(Guid alertId);
        Task DismissAllAlertsAsync();
        Task<IEnumerable<ServerReportsDto>> GetReportsAsync();
        Task<Controllers.API.AboutDto> GetAboutAsync();
        Task<List<Controllers.API.CommandGroupDto>> GetHelpAsync();

        Task<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>> GetClientMetaAsync(
            int clientId, int count, int offset, long? startAt, SharedLibraryCore.Interfaces.MetaType? metaType);

        Task<Controllers.API.InteractionResponse> GetInteractionAsync(string interactionName);
        Task<string> UnbanClientAsync(int clientId, string reason);

        Task<IEnumerable<QueryHelpers.Models.ClientResourceResponse>> GetClientsAsync(
            QueryHelpers.Models.ClientResourceRequest request);

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
