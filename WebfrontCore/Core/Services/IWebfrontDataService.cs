using SharedLibraryCore.Dtos;
using Data.Models;
using SharedLibraryCore.Helpers;
using Stats.Dtos;
using WebfrontCore.Components.Features.Admin.Models;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Components.UI.Navigation.Models;
using WebfrontCore.Components.Features.Home.Models;
using WebfrontCore.Components.Features.Console.Models;
using WebfrontCore.Components.Features.Auth.Models;
using PenaltyInfo = SharedLibraryCore.Dtos.PenaltyInfo;

namespace WebfrontCore.Core.Services;

public interface IWebfrontDataService
{
    Task<List<ServerInfo>> GetServersAsync(Reference.Game? game = null);
    Task<ServerInfo?> GetServer(string id);
    Task<IW4MAdminInfo> GetStatusAsync(Reference.Game? game = null);
    Task<NavigationInfo> GetNavigationDataAsync();
    Task<PlayerInfo?> GetClientProfileAsync(int clientId, SharedLibraryCore.Interfaces.MetaType? metaFilterType = null);
    Task<ClientInfoResult> GetClientInfoAsync(int clientId);
    Task<ResourceQueryHelperResult<ClientResourceResponse>> SearchClientsAsync(ClientResourceRequest request);
    Task<ScoreboardInfo?> GetServerScoreboardAsync(string serverId);
    Task<ResourceQueryHelperResult<BanInfo>?> GetBansAsync(BanInfoRequest request);
    Task<IList<AuditInfo>> GetAuditLogAsync(AuditFilterRequest request);
    Task<AuditStatistics> GetAuditStatisticsAsync(AuditFilterRequest request);
    Task<List<CommandResponseInfo>> ExecuteCommandAsync(string serverId, string command);
    Task<IList<PenaltyInfo>> GetPenaltiesAsync(PenaltyRequest request);
    Task<long> GetPenaltiesCountAsync(PenaltyRequest request);
    Task<IEnumerable<ConfigurationFileInfo>> GetConfigurationFilesAsync();
    Task SaveConfigurationFileAsync(string fileName, string content);
    Task<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>> GetPrivilegedClientsAsync();
    Task<TopStatsResponse> GetTopStatsAsync(TopStatsRequest request);
    Task<AdvancedStatsInfo?> GetClientStatisticsAsync(int clientId, string? serverId = null);
    Task<IList<StatsInfoResult>> GetClientStatsAsync(int clientId);
    Task<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync();
    Task DismissAlertAsync(Guid alertId);
    Task DismissAllAlertsAsync();
    Task<IEnumerable<ServerReportsInfo>> GetReportsAsync();
    Task<IEnumerable<ServerAdminsInfo>> GetOnlineAdminsAsync();
    Task<IEnumerable<ServerFlaggedInfo>> GetOnlineFlaggedAsync();
    Task<AboutInfo> GetAboutInfoAsync();
    Task<List<CommandGroupInfo>> GetHelpCommandsAsync();

    Task<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>> GetClientMetaAsync(
        ClientMetaRequest request);

    Task<InteractionResponse?> GetInteractionAsync(string interactionName, Dictionary<string, string>? query = null);
    Task<System.Security.Claims.ClaimsPrincipal> LoginAsync(ServiceLoginRequest request);
    Task<string> UnbanClientAsync(int clientId, string reason);
    Task<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>> GetChatContextAsync(string serverId, long when);
    Task<List<Dictionary<string, string>>> GetAutomatedPenaltyContextAsync(int penaltyId);
    Task<ResourceQueryHelperResult<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>> SearchMessagesAsync(ChatSearchQuery request);
    Task<SystemInfo> GetSystemInfoAsync();
    Task<IEnumerable<ClientCountSnapshot>> GetClientHistoryAsync(string serverId);
    
    /// <summary>
    /// Gets available RCon/Event parser names for server configuration
    /// </summary>
    IEnumerable<string> GetAvailableParsers();
    
    /// <summary>
    /// Adds a new server dynamically at runtime
    /// </summary>
    /// <param name="request">The server configuration request</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Response with server info if successful, null otherwise</returns>
    Task<AddServerResponse?> AddServerAsync(AddServerRequest request, CancellationToken token = default);
    
    /// <summary>
    /// Removes a server dynamically at runtime
    /// </summary>
    /// <param name="serverId">Server identifier (IP:Port or internal ID)</param>
    /// <param name="persist">Whether to persist the removal to configuration</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>True if server was removed successfully</returns>
    Task<bool> RemoveServerAsync(string serverId, bool persist = false, CancellationToken token = default);
    
    Task<AnnouncementInfo?> GetActiveAnnouncementAsync(bool globalOnly = false);
    Task<IEnumerable<AnnouncementInfo>> GetAllAnnouncementsAsync();
    Task<AnnouncementInfo> CreateAnnouncementAsync(CreateAnnouncementRequest request, int createdByClientId);
    Task<AnnouncementInfo> UpdateAnnouncementAsync(UpdateAnnouncementRequest request);
    Task DeleteAnnouncementAsync(int id);
    Task ActivateAnnouncementAsync(int id);
    Task DeactivateAnnouncementAsync(int id);

    Task<TwoFactorSetupInfo> EnableTwoFactorAsync();
    Task<TwoFactorConfirmResponse> ConfirmTwoFactorAsync(string secret, string code);
    Task DisableTwoFactorAsync();
    Task<bool> ValidateTwoFactorCodeAsync(int clientId, string code);
}
