using SharedLibraryCore;
using System.Diagnostics;
using SharedLibraryCore.Dtos;
using Data.Models;
using SharedLibraryCore.Helpers;

using WebfrontCore.Components.Features.Admin.Models;
using WebfrontCore.Components.Features.Servers.Models;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Components.UI.Navigation.Models;
using WebfrontCore.Components.Features.Home.Models;
using WebfrontCore.Components.Features.Console.Models;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using PenaltyInfo = SharedLibraryCore.Dtos.PenaltyInfo;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using SharedLibraryCore.Services;
using Stats.Dtos;
using WebfrontCore.Core.Auth;
using System.Security.Claims;
using SharedLibraryCore.Events.Management;

namespace WebfrontCore.Core.Services;

public class WebfrontDataService : IWebfrontDataService
{
    private readonly IManager _manager;
    private readonly IServerDataViewer _serverDataViewer;
    private readonly IResourceQueryHelper<BanInfoRequest, BanInfo> _banQueryHelper;
    private readonly IResourceQueryHelper<ChatSearchQuery, MessageResponse> _chatQueryHelper;
    private readonly ClientService _clientService;
    private readonly IMetaServiceV2 _metaService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly IResourceQueryHelper<FindClientRequest, FindClientResult> _findClientHelper;
    private readonly IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse> _clientResourceHelper;
    private readonly IResourceQueryHelper<StatsInfoRequest, StatsInfoResult> _statsHelper;
    private readonly IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> _advancedStatsHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly StatManager _statManager;
    private readonly Stats.Config.StatsConfiguration _statsConfig;
    private readonly Data.Abstractions.IDatabaseContextFactory _contextFactory;
    private readonly IAuditInformationRepository _auditRepository;
    private readonly IAlertManager _alertManager;
    private readonly IRemoteCommandService _remoteCommandService;
    private readonly ITranslationLookup _translationLookup;
    private readonly SharedLibraryCore.Configuration.ApplicationConfiguration _appConfig;
    private readonly ILookup<Type, string> _pluginTypeNames;

    public WebfrontDataService(IManager manager,
        IServerDataViewer serverDataViewer,
        IResourceQueryHelper<BanInfoRequest, BanInfo> banQueryHelper,
        IResourceQueryHelper<ChatSearchQuery, MessageResponse> chatQueryHelper,
        ClientService clientService,
        IMetaServiceV2 metaService,
        IGeoLocationService geoLocationService,
        IInteractionRegistration interactionRegistration,
        IResourceQueryHelper<FindClientRequest, FindClientResult> findClientHelper,
        IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse> clientResourceHelper,
        IResourceQueryHelper<StatsInfoRequest, StatsInfoResult> statsHelper,
        IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo> advancedStatsHelper,
        IHttpContextAccessor httpContextAccessor,
        StatManager statManager,
        Stats.Config.StatsConfiguration statsConfig,
        Data.Abstractions.IDatabaseContextFactory contextFactory,
        IAuditInformationRepository auditRepository,
        IAlertManager alertManager,
        IRemoteCommandService remoteCommandService,
        ITranslationLookup translationLookup,
        SharedLibraryCore.Configuration.ApplicationConfiguration appConfig,
        IEnumerable<IPlugin> v1Plugins,
        IEnumerable<IPluginV2> v2Plugins)
    {
        _manager = manager;
        _serverDataViewer = serverDataViewer;
        _banQueryHelper = banQueryHelper;
        _chatQueryHelper = chatQueryHelper;
        _clientService = clientService;
        _metaService = metaService;
        _geoLocationService = geoLocationService;
        _interactionRegistration = interactionRegistration;
        _findClientHelper = findClientHelper;
        _clientResourceHelper = clientResourceHelper;
        _statsHelper = statsHelper;
        _advancedStatsHelper = advancedStatsHelper;
        _httpContextAccessor = httpContextAccessor;
        _statManager = statManager;
        _statsConfig = statsConfig;
        _contextFactory = contextFactory;
        _auditRepository = auditRepository;
        _alertManager = alertManager;
        _remoteCommandService = remoteCommandService;
        _translationLookup = translationLookup;
        _appConfig = appConfig;
        _pluginTypeNames = v1Plugins.Select(plugin => (plugin.GetType(), plugin.Name))
            .Concat(v2Plugins.Select(plugin => (plugin.GetType(), plugin.Name)))
            .ToLookup(selector => selector.Item1, selector => selector.Name);
    }

    public async Task<List<ServerInfo>> GetServersAsync(Reference.Game? game = null)
    {
        var servers = _manager.GetServers()
            .Where(server => game is null || server.GameName == (Server.Game?)game)
            .ToList();

        var clientHistories =
            await _serverDataViewer.ClientHistoryAsync(
                _manager.GetApplicationSettings().Configuration().MaxClientHistoryTime);

        return (from server in servers
            let history = clientHistories.FirstOrDefault(h => h.ServerId == server.LegacyDatabaseId)
            select new ServerInfo
            {
                Name = server.Hostname,
                Id = server.Id,
                Port = server.ListenPort,
                Map = server.CurrentMap?.Alias,
                Game = (Reference.Game)server.GameName,
                ClientCount = server.ClientNum,
                MaxClients = server.MaxClients,
                PrivateClientSlots = server.PrivateClientSlots,
                GameType = server.GametypeName,
                ClientHistory = new ClientHistoryInfo
                {
                    ClientCounts = history?.ClientCounts?.Select(historyItem => new ClientCountSnapshot
                        {
                            Time = historyItem.Time,
                            ClientCount = historyItem.ClientCount,
                            ConnectionInterrupted = historyItem.ConnectionInterrupted,
                            Map = historyItem.Map,
                            MapAlias = server.Maps.FirstOrDefault(map => map.Name == historyItem.Map)?.Alias ??
                                       historyItem.Map
                        })
                        .ToList() ?? []
                },
                Players = server.GetClientsAsList()
                    .Select(client => new PlayerInfo
                    {
                        Name = client.Name,
                        ClientId = client.ClientId,
                        Level = client.Level.ToLocalizedLevelName(),
                        LevelInt = (int)client.Level,
                        Tag = client.Tag,
                        ZScore = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY)?.ZScore
                    })
                    .ToList(),
                ChatHistory = server.ChatHistory.ToList(),
                Online = !server.Throttled,
                IPAddress = server.ListenAddress,
                ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal()
                    ? _manager.ExternalIPAddress
                    : server.ListenAddress,
                ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                    server.ResolvedIpEndPoint.Address.IsInternal()
                        ? _manager.ExternalIPAddress
                        : server.ListenAddress, server.ListenPort)
            }).ToList();
    }

    public Task<ServerInfo?> GetServer(string id)
    {
        var server = _manager.GetServers().FirstOrDefault(s => s.Id == id);
        if (server == null)
            return Task.FromResult<ServerInfo?>(null);

        return Task.FromResult<ServerInfo?>(new ServerInfo
        {
            Name = server.Hostname,
            Id = server.Id,
            Port = server.ListenPort,
            Map = server.CurrentMap?.Alias,
            Game = (Reference.Game)server.GameName,
            ClientCount = server.ClientNum,
            MaxClients = server.MaxClients,
            PrivateClientSlots = server.PrivateClientSlots,
            GameType = server.GametypeName,
            ClientHistory = new ClientHistoryInfo
            {
                ClientCounts = server.ClientHistory.ClientCounts.ToList()
            },
            Players = server.GetClientsAsList()
                .Select(client => new PlayerInfo
                {
                    Name = client.Name,
                    ClientId = client.ClientId,
                    Level = client.Level.ToLocalizedLevelName(),
                    LevelInt = (int)client.Level,
                    Tag = client.Tag,
                    ZScore = client.GetAdditionalProperty<EFClientStatistics>(StatManager
                        .CLIENT_STATS_KEY)?.ZScore
                }).ToList(),
            ChatHistory = server.ChatHistory.ToList(),
            Online = !server.Throttled,
            IPAddress = server.ListenAddress,
            ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal()
                ? _manager.ExternalIPAddress
                : server.ListenAddress,
            ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                server.ResolvedIpEndPoint.Address.IsInternal() ? _manager.ExternalIPAddress : server.ListenAddress,
                server.ListenPort)
        });
    }

    public async Task<IW4MAdminInfo> GetStatusAsync(Reference.Game? game = null)
    {
        var servers = _manager.GetServers()
            .Where(server => game is null || server.GameName == (Server.Game?)game)
            .ToList();
        var (clientCount, time) =
            await _serverDataViewer.MaxConcurrentClientsAsync(gameCode: game);
        var (count, recentCount) =
            await _serverDataViewer.ClientCountsAsync(gameCode: game);

        return new IW4MAdminInfo
        {
            TotalAvailableClientSlots = servers.Sum(server => server.MaxClients),
            TotalOccupiedClientSlots = servers.SelectMany(server => server.GetClientsAsList()).Count(),
            TotalClientCount = count,
            RecentClientCount = recentCount,
            MaxConcurrentClients = clientCount ?? 0,
            MaxConcurrentClientsTime = time ?? DateTime.UtcNow,
            Game = game,
            ActiveServerGames = _manager.GetServers().Select(server => (Reference.Game)server.GameName).Distinct()
                .ToArray(),
            CommandPrefix = _manager.GetApplicationSettings().Configuration().CommandPrefix
        };
    }

    public async Task<NavigationInfo> GetNavigationDataAsync()
    {
        // Get pages from Manager's page list (IDictionary<string, string> where key=name, value=location)
        var rawPages = _manager.GetPageList().Pages;
        var pages = rawPages
            .Select(kvp => new Page { Name = kvp.Key, Location = kvp.Value })
            .ToList();

        // Get all navigation interactions (Main, Admin, Social)
        var interactions = (await _interactionRegistration.GetInteractions("Webfront::Nav"))
            .Select(i => new NavigationInteractionInfo
            {
                InteractionId = i.InteractionId,
                MinimumPermission = (int)(i.MinimumPermission ?? Data.Models.Client.EFClient.Permission.User),
                Name = i.Name,
                DisplayMeta = i.DisplayMeta
            })
            .ToList();

        var user = new ClientInfo
        {
            ClientId = -1,
            Name = "Webfront User",
            Level = Data.Models.Client.EFClient.Permission.User,
        };
        var currentUser = await GetExecutorAsync();
        var authorized = currentUser is not null;

        if (authorized)
        {
            user = new ClientInfo
            {
                ClientId = currentUser!.ClientId,
                Name = currentUser.CurrentAlias?.Name ?? "Unknown",
                Level = currentUser.Level,
                Game = currentUser.GameName
            };
        }

        var localization = Utilities.CurrentLocalization.LocalizationIndex.Set;

        return new NavigationInfo
        {
            User = user,
            Authorized = authorized,
            Localization = localization,
            Pages = pages
                .Select(page => new Page { Name = page.Name, Location = page.Location }),
            Interactions = interactions
                .Select(i => new NavigationInteractionInfo
                {
                   InteractionId = i.InteractionId,
                   MinimumPermission = i.MinimumPermission,
                   Name = i.Name,
                   DisplayMeta = i.DisplayMeta
                }),
            CommunityInformation = new CommunityInfo
            {
                IsEnabled = _manager.GetApplicationSettings().Configuration().CommunityInformation?.IsEnabled ?? false,
                SocialAccounts = (_manager.GetApplicationSettings().Configuration().CommunityInformation?.SocialAccounts?
                    .Select(s => new SocialAccountInfo
                    {
                        Title = s.Title,
                        Url = s.Url,
                        IconId = s.IconId,
                        IconUrl = s.IconUrl
                    }) ?? []).ToArray()
            },
            TotalClientCount = _manager.GetServers().Sum(server => server.ClientNum),
            TotalAdminCount = _manager.GetServers().Sum(server =>
                server.GetClientsAsList()
                    .Count(client => client.Level >= Data.Models.Client.EFClient.Permission.Trusted)),
            TotalReportCount = _manager.GetServers().Sum(server =>
                server.Reports.Count(report =>
                    DateTime.UtcNow - report.ReportedOn <= TimeSpan.FromHours(24)))
        };
    }

    public async Task<PlayerInfo?> GetClientProfileAsync(int clientId,
        MetaType? metaFilterType = null)
    {
        var client = await _clientService.Get(clientId);

        if (client is null)
        {
            return null;
        }

        var activePenalties = await _manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId,
            client.CurrentAliasId, client.NetworkId, client.GameName, client.IPAddress);

        var persistentMetaTask = new[]
        {
            _metaService.GetPersistentMetaByLookup(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2, client.ClientId),
            _metaService.GetPersistentMeta("GravatarEmail", client.ClientId),
        };

        var persistentMeta = await Task.WhenAll(persistentMetaTask);
        var tag = persistentMeta[0];
        var gravatar = persistentMeta[1];
        var note = await _metaService.GetPersistentMetaValue<ClientNoteMetaResponse>("ClientNotes", client.ClientId);

        if (tag?.Value != null)
        {
            client.SetAdditionalProperty(EFMeta.ClientTagV2, tag.Value);
        }

        if (!string.IsNullOrWhiteSpace(note?.Note))
        {
            note.OriginEntityName = await _clientService.GetClientNameById(note.OriginEntityId);
        }

        var interactions =
            await _interactionRegistration.GetInteractions("Webfront::Profile", clientId, client.GameName);

        var hasActiveBan = activePenalties.Any(penalty => penalty.Type == EFPenalty.PenaltyType.Ban);
        if (hasActiveBan)
        {
            client.Level = Data.Models.Client.EFClient.Permission.Banned;
        }

        var displayLevelInt = (int)client.Level;
        var displayLevel = client.Level.ToLocalizedLevelName();

        var shouldHideBanLevel = !hasActiveBan && client.Level == Data.Models.Client.EFClient.Permission.Banned;
        var authorized = _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false; // Simplified

        if (!authorized && client.Level.ShouldHideLevel() || shouldHideBanLevel)
        {
            displayLevelInt = (int)Data.Models.Client.EFClient.Permission.User;
            displayLevel = Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName();
        }

        displayLevel = string.IsNullOrEmpty(client.Tag) ? displayLevel : $"{displayLevel} ({client.Tag})";
        var ingameClient = _manager.GetActiveClients().FirstOrDefault(c => c.ClientId == client.ClientId);

        var geoLocation = await _geoLocationService.Locate(client.IPAddressString);
        GeoLocationInfo? geoLocationInfo = null;
        if (geoLocation != null)
        {
            geoLocationInfo = new GeoLocationInfo
            {
                Country = geoLocation.Country,
                CountryCode = geoLocation.CountryCode,
                Region = geoLocation.Region,
                ASN = geoLocation.ASN,
                Timezone = geoLocation.Timezone,
                Organization = geoLocation.Organization
            };
        }

        var clientDto = new PlayerInfo
        {
            Name = client.Name,
            Game = client.GameName,
            Level = displayLevel,
            LevelInt = displayLevelInt,
            ClientId = client.ClientId,
            IPAddress = client.IPAddressString,
            NetworkId = client.NetworkId,
            Meta = [],
            Aliases = client.AliasLink.Children
                .Select(alias => (alias.Name, alias.DateAdded))
                .GroupBy(alias => alias.Name.StripColors())
                .Select(grp => grp.OrderByDescending(item => item.Name.Length).First())
                .Distinct()
                .Select(a => new ProfileMetaEntry { Value = a.Name, Date = a.DateAdded })
                .ToList(),
            IPs = client.AliasLink.Children // Placeholder: Needs permission check
                .Select(alias => (alias.IPAddress.ConvertIPtoString(), alias.DateAdded))
                .GroupBy(alias => alias.Item1)
                .Select(grp => grp.OrderByDescending(item => item.DateAdded).First())
                .Distinct()
                .Select(i => new ProfileMetaEntry { Value = i.Item1, Date = i.DateAdded })
                .ToList(),
            HasActivePenalty = activePenalties.Any(penalty => penalty.Type != EFPenalty.PenaltyType.Flag),
            Online = ingameClient != null,
            TimeOnline = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
            LinkedAccounts = client.LinkedAccounts,
            MetaFilterType = metaFilterType,
            ConnectProtocolUrl = ingameClient?.CurrentServer.EventParser.URLProtocolFormat.FormatExt(
                ingameClient.CurrentServer.ResolvedIpEndPoint.Address.IsInternal()
                    ? _manager.ExternalIPAddress
                    : ingameClient.CurrentServer.ListenAddress,
                ingameClient.CurrentServer.ListenPort),
            CurrentServerName = ingameClient?.CurrentServer?.Hostname,
            GeoLocationInfo = geoLocationInfo,
            NoteMeta = string.IsNullOrWhiteSpace(note?.Note) ? null : note,
            Interactions = interactions.Select(interaction => new InteractionInfo
            {
                EntityId = interaction.EntityId,
                InteractionId = interaction.InteractionId,
                InteractionType = interaction.InteractionType,
                Enabled = interaction.Enabled,
                Name = interaction.Name,
                Description = interaction.Description,
                DisplayMeta = interaction.DisplayMeta,
                ActionPath = interaction.ActionPath,
                ActionMeta = interaction.ActionMeta,
                ActionUri = interaction.ActionUri,
                MinimumPermission = interaction.MinimumPermission,
                PermissionEntity = interaction.PermissionEntity,
                PermissionAccess = interaction.PermissionAccess,
                Source = interaction.Source
            }).ToList(),
        };

        var config = _manager.GetApplicationSettings().Configuration();
        var executor = await GetExecutorAsync();
        var level = executor?.Level ?? Data.Models.Client.EFClient.Permission.User;

        if (!config.PermissionSets.TryGetValue(level.ToString(), out var permissionSet))
        {
            permissionSet = [];
        }

        var canViewIp = permissionSet.HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read);

        var meta = await _metaService.GetRuntimeMeta<InformationResponse>(new ClientPaginationRequest
        {
            ClientId = client.ClientId,
            Before = DateTime.UtcNow
        }, MetaType.Information);


        if (gravatar != null)
        {
            clientDto.Meta.Add(new InformationResponse()
            {
                Key = "GravatarEmail",
                Type = MetaType.Other,
                Value = gravatar.Value
            });
        }

        clientDto.ActivePenalty = activePenalties.MaxBy(penalty => penalty.Type switch
        {
            EFPenalty.PenaltyType.TempMute => 0,
            EFPenalty.PenaltyType.Mute => 1,
            _ => (int)penalty.Type
        });
        clientDto.Meta.AddRange(authorized ? meta : meta.Where(m => !m.IsSensitive));

        clientDto.IPAddress = canViewIp ? client.IPAddressString : null;
        if (!canViewIp)
        {
            clientDto.IPs = [];
        }
        else
        {
            clientDto.IPs = client.AliasLink.Children
                .Select(alias => (alias.IPAddress.ConvertIPtoString(), alias.DateAdded))
                .GroupBy(alias => alias.Item1)
                .Select(grp => grp.OrderByDescending(item => item.DateAdded).First())
                .Distinct()
                .Select(i => new ProfileMetaEntry { Value = i.Item1, Date = i.DateAdded })
                .ToList();
        }

        return clientDto;
    }

    public async Task<ClientInfoResult> GetClientInfoAsync(int clientId)
    {
        var clientInfo = await _clientService.Get(clientId);
        if (clientInfo is null)
        {
            throw new Exception("Could not find client");
        }

        var metaResult =
            await _metaService.GetPersistentMetaByLookup(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2,
                clientInfo.ClientId);

        return new ClientInfoResult
        {
            ClientId = clientInfo.ClientId,
            Name = clientInfo.CleanedName,
            Level = clientInfo.Level.ToLocalizedLevelName(),
            NetworkId = clientInfo.NetworkId,
            GameName = clientInfo.GameName.ToString(),
            Tag = metaResult?.Value,
            FirstConnection = clientInfo.FirstConnection,
            LastConnection = clientInfo.LastConnection,
            TotalConnectionTime = clientInfo.TotalConnectionTime,
            Connections = clientInfo.Connections,
        };
    }

    public async Task<IEnumerable<BaseMetaResponse>> GetClientMetaAsync(
        int clientId, int count, int offset, long? startAt, MetaType? metaType)
    {
        var request = new ClientPaginationRequest
        {
            ClientId = clientId,
            Count = count,
            Offset = offset,
            Before = startAt.HasValue ? DateTime.FromFileTimeUtc(startAt.Value) : DateTime.UtcNow,
        };

        var config = _manager.GetApplicationSettings().Configuration();
        var user = _httpContextAccessor.HttpContext?.User;
        var level = Data.Models.Client.EFClient.Permission.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var levelClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            if (Enum.TryParse(levelClaim, out Data.Models.Client.EFClient.Permission result))
            {
                level = result;
            }
        }

        if (!config.PermissionSets.TryGetValue(level.ToString(), out var permissionSet))
        {
            permissionSet = [];
        }

        IEnumerable<IClientMeta>? meta;

        if (metaType is null or MetaType.All)
        {
            meta = await _metaService.GetRuntimeMeta(request);
        }
        else
        {
            meta = metaType switch
            {
                MetaType.Information => await _metaService.GetRuntimeMeta<InformationResponse>(request,
                    metaType.Value),
                MetaType.AliasUpdate => permissionSet.HasPermission(WebfrontEntity.MetaAliasUpdate,
                    WebfrontPermission.Read)
                    ? await _metaService.GetRuntimeMeta<UpdatedAliasResponse>(request, metaType.Value)
                    : new List<IClientMeta>(),
                MetaType.ChatMessage => await _metaService.GetRuntimeMeta<MessageResponse>(request, metaType.Value),
                MetaType.Penalized => await _metaService.GetRuntimeMeta<AdministeredPenaltyResponse>(request,
                    metaType.Value),
                MetaType.ReceivedPenalty => await _metaService.GetRuntimeMeta<ReceivedPenaltyResponse>(request,
                    metaType.Value),
                MetaType.ConnectionHistory => await _metaService.GetRuntimeMeta<ConnectionHistoryResponse>(request,
                    metaType.Value),
                MetaType.PermissionLevel => await _metaService.GetRuntimeMeta<PermissionLevelChangedResponse>(
                    request, metaType.Value),
                _ => await _metaService.GetRuntimeMeta(request)
            };
        }

        if (level < Data.Models.Client.EFClient.Permission.Trusted)
        {
            meta = meta?.Where(m => !m.IsSensitive);
        }

        return meta?.Cast<BaseMetaResponse>().ToList() ?? [];
    }

    public Task<ScoreboardInfo?> GetServerScoreboardAsync(string serverId)
    {
        var server = _manager.GetServers()
            .FirstOrDefault(s => s.Id == serverId);
        if (server is null)
            return Task.FromResult<ScoreboardInfo?>(null);

        return Task.FromResult<ScoreboardInfo?>(new ScoreboardInfo
        {
            MapName = server.CurrentMap.ToString(),
            ServerName = server.Hostname,
            ServerId = server.Id,
            GameCode = server.GameCode,
            ClientInfo = server.GetClientsAsList().Select(client =>
                    new
                    {
                        stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY),
                        client
                    })
                .Select(clientData => new ClientScoreboardInfo
                {
                    ClientName = clientData.client.Name,
                    ClientId = clientData.client.ClientId,
                    Score = Math.Max(clientData.client.Score, clientData.stats?.RoundScore ?? 0),
                    Ping = clientData.client.Ping,
                    Kills = clientData.stats?.MatchData?.Kills,
                    Deaths = clientData.stats?.MatchData?.Deaths,
                    ScorePerMinute = clientData.stats?.SessionSPM,
                    Kdr = clientData.stats?.MatchData?.Kdr,
                    ZScore = clientData.stats?.ZScore == null || clientData.stats.ZScore == 0
                        ? null
                        : clientData.stats.ZScore,
                    Team = clientData.client.Team
                })
                .ToList()
        });
    }

    public async Task<ResourceQueryHelperResult<BanInfo>?> GetBansAsync(BanInfoRequest request)
    {
        return await _banQueryHelper.QueryResource(request);
    }

    public async Task<IList<AuditInfo>> GetAuditLogAsync(PaginationRequest request)
    {
        return await _auditRepository.ListAuditInformation(request);
    }

    public async Task<List<CommandResponseInfo>> ExecuteCommandAsync(string serverId, string command)
    {
        var client = await GetExecutorAsync();

        if (client is null || client.ClientId < 1)
        {
            return
            [
                new CommandResponseInfo
                {
                    Response = _translationLookup["SERVER_COMMANDS_INTERCEPTED"]
                }
            ];
        }

        var server = _manager.GetServers().FirstOrDefault(s => s.Id == serverId);
        if (server is null)
        {
            return [new CommandResponseInfo { Response = "Server not found" }];
        }

        var (_, response) = await _remoteCommandService.ExecuteWithResult(client.ClientId, null, command,
            [], server);

        return response.ToList();
    }

    public async Task<IList<PenaltyInfo>> GetPenaltiesAsync(int offset = 0, int count = 30,
        EFPenalty.PenaltyType showOnly = EFPenalty.PenaltyType.Any, bool ignoreAutomated = true)
    {
        var penalties = await _manager.GetPenaltyService().GetRecentPenalties(count, offset, showOnly, ignoreAutomated);
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated != true ? penalties.Where(p => !p.Sensitive).ToList() : penalties;
    }

    public async Task<string> UnbanClientAsync(int clientId, string reason)
    {
        var executor = await GetExecutorAsync();
        if (executor == null)
            return "Unauthorized";

        var targetClient = await _clientService.Get(clientId);
        if (targetClient == null)
            return "Client not found";

        var server = _manager.GetServers().First();
        executor.CurrentServer = server;

        var unbanEvent = targetClient.Unban(reason, executor);
        await unbanEvent.WaitAsync();

        if (!unbanEvent.Failed)
        {
            return unbanEvent.Output.Count > 0
                ? string.Join(" ", unbanEvent.Output)
                : "Client unbanned successfully";
        }

        var msg = unbanEvent.Output.Count > 0
            ? string.Join(" ", unbanEvent.Output)
            : "Unban failed";

        return msg;
    }

    public async Task<TopStatsResponse> GetTopStatsAsync(int count, int offset, string? serverId = null)
    {
        var server = _manager.GetServers().FirstOrDefault(s => s.Id == serverId) as IGameServer;
        var legacyId = server?.LegacyDatabaseId;

        var stats = _statsConfig.EnableAdvancedMetrics
            ? await _statManager.GetNewTopStats(offset, count, legacyId)
            : await _statManager.GetTopStats(offset, count, legacyId);

        var totalRanked = await _serverDataViewer.RankedClientsCountAsync(legacyId);

        return new TopStatsResponse
        {
            Players = stats,
            TotalRankedClients = totalRanked
        };
    }

    public async Task<AdvancedStatsInfo?> GetClientStatisticsAsync(int clientId, string? serverId = null)
    {
        var hitInfo = (await _advancedStatsHelper.QueryResource(new StatsInfoRequest
        {
            ClientId = clientId,
            ServerEndpoint = serverId
        }))?.Results?.First();

        if (hitInfo is null)
        {
            return null;
        }

        IGameServer? server = _manager.GetServers().FirstOrDefault(s => s.Id == serverId);
        var matchedServerId = server?.LegacyDatabaseId;

        hitInfo.TotalRankedClients = await _serverDataViewer.RankedClientsCountAsync(matchedServerId);
        return hitInfo;
    }

    public async Task<IList<StatsInfoResult>> GetClientStatsAsync(int clientId)
    {
        var request = new StatsInfoRequest { ClientId = clientId };
        var result = await _statsHelper.QueryResource(request);
        return result.Results.ToList();
    }

    public async Task<IEnumerable<ConfigurationFileInfo>> GetConfigurationFilesAsync()
    {
        try
        {
            var files = await Task.WhenAll(Directory
                .GetFiles(Path.Join(Utilities.OperatingDirectory, "Configuration"))
                .Where(file => file.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                .Select(async fileName => new ConfigurationFileInfo
                {
                    FileName = fileName.Split(Path.DirectorySeparatorChar).Last(),
                    FileContent = await File.ReadAllTextAsync(fileName)
                }));
            return files;
        }
        catch (Exception)
        {
            return new List<ConfigurationFileInfo>();
        }
    }

    public async Task SaveConfigurationFileAsync(string fileName, string content)
    {
        if (!fileName.EndsWith(".json"))
        {
            throw new ArgumentException("File must be of json format.");
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("File content cannot be empty");
        }

        try
        {
            System.Text.Json.JsonDocument.Parse(content);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ArgumentException($"File is not valid. {fileName}: {ex.Message}");
        }

        var path = Path.Join(Utilities.OperatingDirectory, "Configuration",
            fileName.Replace($"{Path.DirectorySeparatorChar}", ""));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{fileName} does not exist");
        }

        await File.WriteAllTextAsync(path, content);
    }

    public async Task<Dictionary<Data.Models.Client.EFClient.Permission, IList<ClientInfo>>> GetPrivilegedClientsAsync()
    {
        var admins = (await _clientService.GetPrivilegedClients())
            .Select(client => new ClientInfo
            {
                Name = client.Name,
                ClientId = client.ClientId,
                Level = client.Level,
                LastConnection = client.LastConnection,
                Game = client.GameName
            })
            .GroupBy(client => client.Level)
            .ToDictionary(folder => folder.Key, IList<ClientInfo> (folder) => folder.ToList());
        return admins;
    }

    public async Task<FindClientResponse> SearchClientsAsync(FindClientRequest request)
    {
        var results = await _findClientHelper.QueryResource(request);
        return new FindClientResponse
        {
            Clients = results.Results.ToList(),
            TotalFoundClients = results.RetrievedResultCount
        };
    }

    public async Task<IEnumerable<SharedLibraryCore.Alerts.Alert.AlertState>> GetAlertsAsync()
    {
        var client = await GetExecutorAsync();
        return client == null ? [] : _alertManager.RetrieveAlerts(client);
    }

    public Task DismissAlertAsync(Guid alertId)
    {
        _alertManager.MarkAlertAsRead(alertId);
        return Task.CompletedTask;
    }

    public async Task DismissAllAlertsAsync()
    {
        var client = await GetExecutorAsync();
        if (client != null)
        {
            _alertManager.MarkAllAlertsAsRead(client.ClientId);
        }
    }


    public Task<IEnumerable<ServerReportsInfo>> GetReportsAsync()
    {
        var reports = _manager.GetServers()
            .Select(server => new ServerReportsInfo
            {
                Id = server.LegacyDatabaseId,
                Name = server.Hostname,
                Reports = server.Reports.Select(r => new ReportInfo
                {
                    Target = new ReportEntityInfo { Name = r.Target.Name, ClientId = r.Target.ClientId },
                    Origin = new ReportEntityInfo { Name = r.Origin.Name, ClientId = r.Origin.ClientId },
                    Reason = r.Reason,
                    ReportedOn = r.ReportedOn
                }).OrderByDescending(r => r.ReportedOn).ToList()
            })
            .Where(s => s.Reports.Any())
            .ToList();
        return Task.FromResult<IEnumerable<ServerReportsInfo>>(reports);
    }

    public Task<AboutInfo> GetAboutInfoAsync()
    {
        var activeServers = _appConfig.Servers.Where(server =>
            _manager.GetServers()
                .FirstOrDefault(s => s.ListenAddress == server.IPAddress && s.ListenPort == server.Port) != null);

        var serverRules = activeServers.Select(config =>
        {
            var server = _manager.GetServers().First(server =>
                server.ListenAddress == config.IPAddress && server.ListenPort == config.Port);
            return new ServerRulesInfo
            {
                ServerName = server.ServerName,
                IPAddress = server.ListenAddress,
                Port = server.Port,
                Rules = config.Rules
            };
        }).ToList();

        var about = new AboutInfo
        {
            CommunityInformation = _appConfig.CommunityInformation,
            GlobalRules = _appConfig.GlobalRules,
            ServerRules = serverRules
        };
        return Task.FromResult(about);
    }

    public async Task<List<CommandGroupInfo>> GetHelpCommandsAsync()
    {
        var user = await GetExecutorAsync();
        var userLevel = user != null
            ? Data.Models.Client.EFClient.Permission.Owner
            : Data.Models.Client.EFClient.Permission.User;
        if (user != null)
        {
            userLevel = user.Level;
        }

        var commands = _manager.GetCommands()
            .Where(command => command.Permission <= userLevel)
            .OrderByDescending(command => command.Permission)
            .GroupBy(command =>
            {
                if (command.GetType().Name == "ScriptCommand")
                {
                    return _translationLookup["WEBFRONT_HELP_SCRIPT_PLUGIN"];
                }

                var assemblyName = command.GetType().Assembly.GetName().Name;
                if (assemblyName is "IW4MAdmin" or "SharedLibraryCore")
                {
                    return _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                }

                var pluginType = command.GetType().Assembly.GetTypes()
                    .FirstOrDefault(type =>
                        typeof(IPlugin).IsAssignableFrom(type) || typeof(IPluginV2).IsAssignableFrom(type));

                if (pluginType == null)
                {
                    return _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                }

                return _pluginTypeNames[pluginType].FirstOrDefault() ??
                       _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
            })
            .Select(group => new CommandGroupInfo
            {
                Name = group.Key,
                Commands = group.Select(c => new CommandInfo
                {
                    Name = c.Name,
                    Alias = c.Alias,
                    Description = c.Description,
                    Syntax = c.Syntax,
                    RequiresTarget = c.RequiresTarget,
                    Permission = c.Permission,
                    SupportedGames = c.SupportedGames
                }).ToList()
            }).ToList();

        return commands;
    }

    public async Task<InteractionResponse?> GetInteractionAsync(string interactionName,
        string? query = null)
    {
        var interactionData = (await _interactionRegistration.GetInteractions(interactionName)).FirstOrDefault();

        if (interactionData is null)
        {
            return null;
        }

        var user = await GetExecutorAsync();

        if ((user?.Level ?? Data.Models.Client.EFClient.Permission.User) < interactionData.MinimumPermission)
        {
            return null;
        }

        var meta = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(query))
        {
            try
            {
                var q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query);
                foreach (var kvp in q)
                {
                    meta[kvp.Key] = kvp.Value!;
                }
            }
            catch
            {
                // ignored
            }
        }

        var result =
            await _interactionRegistration.ProcessInteraction(interactionName, user?.ClientId ?? 0, meta: meta);

        return new InteractionResponse
        {
            Title = interactionData.Description ?? interactionData.Name,
            Content = result ?? "",
            InteractionType = interactionData.InteractionType.ToString(),
            DisplayMeta = interactionData.DisplayMeta
        };
    }

    private async Task<EFClient?> GetExecutorAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var sidClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid);
        if (sidClaim != null && int.TryParse(sidClaim.Value, out var clientId))
        {
            return await _clientService.Get(clientId);
        }

        return null;
    }

    public async Task<IEnumerable<ClientResourceResponse>> GetClientsAsync(ClientResourceRequest request)
    {
        var executor = await GetExecutorAsync();
        request.RequesterPermission = executor?.Level ?? Data.Models.Client.EFClient.Permission.User;

        var config = _manager.GetApplicationSettings().Configuration();
        if (config.PermissionSets.TryGetValue(request.RequesterPermission.ToString(), out var permissionSet))
        {
            if (!permissionSet.HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read))
            {
                request.ClientIp = null;
            }

            if (!permissionSet.HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read))
            {
                request.ClientGuid = null;
            }
        }
        else
        {
            request.ClientIp = null;
            request.ClientGuid = null;
        }

        var results = await _clientResourceHelper.QueryResource(request);
        return results.Results;
    }

    public async Task<List<MessageResponse>> GetChatContextAsync(string serverId,
        long when)
    {
        var whenTime = DateTime.FromFileTimeUtc(when);
        var whenUpper = whenTime.AddMinutes(5);
        var whenLower = whenTime.AddMinutes(-5);

        var messages = await _chatQueryHelper.QueryResource(new ChatSearchQuery
        {
            ServerId = serverId,
            SentBefore = whenUpper,
            SentAfter = whenLower
        });

        return messages.Results.OrderBy(message => message.When).ToList();
    }

    public async Task<List<Dictionary<string, string>>> GetAutomatedPenaltyContextAsync(int penaltyId)
    {
        await using var context = _contextFactory.CreateContext(false);
        var penalty = await context.Penalties
            .Select(p => new
                { p.OffenderId, p.PenaltyId, p.When, p.AutomatedOffense })
            .FirstOrDefaultAsync(p => p.PenaltyId == penaltyId);

        if (penalty == null)
        {
            return [];
        }

        var iqSnapshotInfo = context.ACSnapshots
            .Where(s => s.ClientId == penalty.OffenderId)
            .Include(s => s.LastStrainAngle)
            .Include(s => s.HitOrigin)
            .Include(s => s.HitDestination)
            .Include(s => s.CurrentViewAngle)
            .Include(s => s.Server)
            .Include(s => s.PredictedViewAngles)
            .ThenInclude(angles => angles.Vector)
            .OrderBy(s => s.When)
            .ThenBy(s => s.Hits);

        var penaltyInfo = await iqSnapshotInfo.ToListAsync();

        if (penaltyInfo.Count <= 0)
        {
            return
            [
                new Dictionary<string, string>
                {
                    ["ClientId"] = penalty.OffenderId.ToString(),
                    ["Message"] = penalty.AutomatedOffense,
                    ["When"] = penalty.When.ToStandardFormat()
                }
            ];
        }

        var formattedInfo =
            new List<Dictionary<string, string>>();

        foreach (var snapshot in penaltyInfo)
        {
            var snapshotDict = new Dictionary<string, string>();
            var props = snapshot.GetType().GetProperties().OrderBy(prop => prop.Name);

            foreach (var prop in props)
            {
                if ((prop.Name.EndsWith("Id") && prop.Name != "WeaponId" || prop.Name == "Server") ||
                    new[] { "Active", "Client", "PredictedViewAngles" }.Contains(prop.Name))
                {
                    continue;
                }

                var value = prop.GetValue(snapshot)?.ToString()?.StripColors() ?? string.Empty;
                snapshotDict.Add(prop.Name, value);
            }

            formattedInfo.Add(snapshotDict);
        }

        return formattedInfo;
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var duration = TimeSpan.FromHours(24);
        var (totalClients, totalRecentClients) = await _serverDataViewer.ClientCountsAsync(duration, null, CancellationToken.None);
        var (maxConcurrent, maxConcurrentTime) = await _serverDataViewer.MaxConcurrentClientsAsync(overPeriod: duration, token: CancellationToken.None);
        var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
        
        return new SystemInfo
        {
            TotalTrackedClients = totalClients,
            TotalConnectedClients = _manager.GetActiveClients().Count,
            TotalClientSlots = _manager.GetServers().Sum(server => server.MaxClients),
            MaxConcurrentClients = new SystemInfo.MetricSnapshot<int?>
            {
                Value = maxConcurrent,
                Time = maxConcurrentTime,
                EndAt = DateTime.UtcNow,
                StartAt = DateTime.UtcNow - duration
            },
            TotalRecentClients = new SystemInfo.MetricSnapshot<int>
            {
                Value = totalRecentClients,
                EndAt = DateTime.UtcNow,
                StartAt = DateTime.UtcNow - duration
            },
            Uptime = uptime,
        };
    }

    public async Task<IEnumerable<ClientCountSnapshot>> GetClientHistoryAsync(string serverId)
    {
        var foundServer = _manager.GetServers().FirstOrDefault(server => server.Id == serverId);

        if (foundServer is null)
        {
            return [];
        }

        var clientHistory = (await _serverDataViewer.ClientHistoryAsync(_appConfig.MaxClientHistoryTime, CancellationToken.None))?
            .FirstOrDefault(history => history.ServerId == foundServer.LegacyDatabaseId) ??
            new ClientHistoryInfo
            {
                ServerId = foundServer.LegacyDatabaseId,
                ClientCounts = []
            };

        var counts = clientHistory.ClientCounts?.AsEnumerable() ?? [];

        if (foundServer.ClientHistory.ClientCounts.Count is not 0)
        {
            counts = counts.Union(foundServer.ClientHistory.ClientCounts.Where(history =>
                    history.Time > (clientHistory.ClientCounts?.LastOrDefault()?.Time ?? DateTime.MinValue)))
                .Where(history => history.Time >= DateTime.UtcNow - _appConfig.MaxClientHistoryTime);
        }

        var clientCountSnapshots = counts.ToList();
        if (foundServer.Maps.Count <= 0)
        {
            return clientCountSnapshots;
        }

        foreach (var count in clientCountSnapshots)
        {
            count.MapAlias = foundServer.Maps.FirstOrDefault(map => map.Name == count.Map)?.Alias ?? count.Map;
        }

        return clientCountSnapshots;
    }
    
    public async Task<InteractionResponse?> GetInteractionAsync(string interactionName, Dictionary<string, string>? query = null)
    {
        var interactionData = (await _interactionRegistration.GetInteractions(interactionName, token: CancellationToken.None)).FirstOrDefault();

        if (interactionData is null)
        {
            return null;
        }
        
        var executor = await GetExecutorAsync();

        if ((executor?.Level ?? Data.Models.Client.EFClient.Permission.User) < interactionData.MinimumPermission)
        {
             throw new UnauthorizedAccessException("Insufficient permission to execute interaction");
        }
        
        var result = await _interactionRegistration.ProcessInteraction(interactionName, executor?.ClientId ?? 0, meta: query ?? new Dictionary<string, string>(), token: CancellationToken.None);

        return new InteractionResponse
        {
            Title = interactionData.Description ?? interactionData.Name,
            Content = result ?? "",
            InteractionType = interactionData.InteractionType.ToString(),
            DisplayMeta = interactionData.DisplayMeta
        };
    }

    public async Task<ClaimsPrincipal> LoginAsync(int clientId, string password, string ipAddress)
    {
        if (clientId is 0)
        {
             throw new UnauthorizedAccessException("Invalid Client ID");
        }

        var privilegedClient = await _clientService.GetClientForLogin(clientId);
        
        var tokenData = new TokenIdentifier
        {
            ClientId = clientId,
            Token = password
        };

        var loginSuccess = _manager.TokenAuthenticator.AuthorizeToken(tokenData) ||
                       (await Task.FromResult(Hashing.Hash(password, privilegedClient.PasswordSalt)))[0] == privilegedClient.Password;

        if (!loginSuccess)
        {
             throw new UnauthorizedAccessException("Invalid credentials");
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, privilegedClient.Name),
            new(ClaimTypes.Role, privilegedClient.Level.ToString()),
            new(ClaimTypes.Sid, privilegedClient.ClientId.ToString()),
            new(ClaimTypes.PrimarySid, privilegedClient.NetworkId.ToString("X")),
            new(ClaimTypes.PrimaryGroupSid, privilegedClient.GameName.ToString())
        ];

        var claimsIdentity = new ClaimsIdentity(claims, "login");
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        _manager.AddEvent(new GameEvent
        {
            Origin = privilegedClient,
            Type = GameEvent.EventType.Login,
            Owner = _manager.GetServers().First(),
            Data = ipAddress
        });

        _manager.QueueEvent(new LoginEvent
        {
            Source = this,
            LoginSource = LoginEvent.LoginSourceType.Webfront,
            EntityId = clientId.ToString(),
            Identifier = ipAddress
        });

        return claimsPrincipal;
    }
}
