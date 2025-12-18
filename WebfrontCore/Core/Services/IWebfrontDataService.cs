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
    Task<ScoreboardInfo?> GetServerScoreboardAsync(string serverId);
    Task<ResourceQueryHelperResult<BanInfo>?> GetBansAsync(BanInfoRequest request);
    Task<IList<AuditInfo>> GetAuditLogAsync(PaginationRequest request);
    Task<List<CommandResponseInfo>> ExecuteCommandAsync(string serverId, string command);
    Task<IList<PenaltyInfo>> GetPenaltiesAsync(int offset = 0, int count = 30,
        EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true);
    Task<IEnumerable<ConfigurationFileInfo>> GetConfigurationFilesAsync();
    Task<string> SaveConfigurationFileAsync(string fileName, string content);
    Task<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>> GetPrivilegedClientsAsync();
    Task<FindClientResponse> SearchClientsAsync(FindClientRequest request);
    Task<TopStatsResponse> GetTopStatsAsync(int count, int offset, string? serverId = null);
    Task<AdvancedStatsInfo?> GetClientStatisticsAsync(int clientId, string? serverId = null);
    Task<IList<StatsInfoResult>> GetClientStatsAsync(int clientId);
    Task<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync();
    Task DismissAlertAsync(Guid alertId);
    Task DismissAllAlertsAsync();
    Task<IEnumerable<ServerReportsInfo>> GetReportsAsync();
    Task<AboutInfo> GetAboutInfoAsync();
    Task<List<CommandGroupInfo>> GetHelpCommandsAsync();
    Task<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>> GetClientMetaAsync(
        int clientId, int count, int offset, long? startAt, SharedLibraryCore.Interfaces.MetaType? metaType);
    Task<Controllers.API.InteractionResponse?> GetInteractionAsync(string interactionName, string? query = null);
    Task<string> UnbanClientAsync(int clientId, string reason);
    Task<IEnumerable<ClientResourceResponse>> GetClientsAsync(ClientResourceRequest request);
    Task<List<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>> GetChatContextAsync(string serverId, long when);
    Task<List<Dictionary<string, string>>> GetAutomatedPenaltyContextAsync(int penaltyId);
}
