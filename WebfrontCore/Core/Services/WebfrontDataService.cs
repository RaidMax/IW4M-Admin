using SharedLibraryCore;
using System.Diagnostics;
using SharedLibraryCore.Dtos;
using Data.Models;
using SharedLibraryCore.Configuration;
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
using IW4MAdmin.Plugins.Stats.Web.Dtos;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using SharedLibraryCore.Services;
using Stats.Dtos;
using WebfrontCore.Core.Auth;
using System.Security.Claims;
using SharedLibraryCore.Events.Management;
using WebfrontCore.Components.Features.Auth.Models;

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
    private readonly ApplicationConfiguration _appConfig;
    private readonly ILookup<Type, string> _pluginTypeNames;
    private readonly IAnnouncementService _announcementService;
    private readonly ITwoFactorAuthService _twoFactorService;

    public WebfrontDataService(IManager manager,
        IServerDataViewer serverDataViewer,
        IResourceQueryHelper<BanInfoRequest, BanInfo> banQueryHelper,
        IResourceQueryHelper<ChatSearchQuery, MessageResponse> chatQueryHelper,
        ClientService clientService,
        IMetaServiceV2 metaService,
        IGeoLocationService geoLocationService,
        IInteractionRegistration interactionRegistration,
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
        ApplicationConfiguration appConfig,
        IEnumerable<IPlugin> v1Plugins,
        IEnumerable<IPluginV2> v2Plugins,
        IAnnouncementService announcementService,
        ITwoFactorAuthService twoFactorService)
    {
        _manager = manager;
        _serverDataViewer = serverDataViewer;
        _banQueryHelper = banQueryHelper;
        _chatQueryHelper = chatQueryHelper;
        _clientService = clientService;
        _metaService = metaService;
        _geoLocationService = geoLocationService;
        _interactionRegistration = interactionRegistration;
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
        _pluginTypeNames = v1Plugins.Where(plugin => plugin is not UnavailablePlugin)
            .Select(plugin => (plugin.GetType(), plugin.Name))
            .Concat(v2Plugins.Where(plugin => plugin is not UnavailablePlugin)
                .Select(plugin => (plugin.GetType(), plugin.Name)))
            .ToLookup(selector => selector.Item1, selector => selector.Name);
        _announcementService = announcementService;
        _twoFactorService = twoFactorService;
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
                    ClientCounts = GetCombinedClientHistory(
                        history?.ClientCounts?.Select(historyItem => new ClientCountSnapshot
                        {
                            Time = historyItem.Time,
                            ClientCount = historyItem.ClientCount,
                            ConnectionInterrupted = historyItem.ConnectionInterrupted,
                            Map = historyItem.Map,
                            MapAlias = server.Maps.FirstOrDefault(map => map.Name == historyItem.Map)?.Alias ??
                                       historyItem.Map
                        }).ToList(),
                        server.ClientHistory.ClientCounts,
                        _manager.GetApplicationSettings().Configuration().MaxClientHistoryTime
                    )
                },
                Players = server.GetClientsAsList()
                    .Select(client => new
                    {
                        client,
                        stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY)
                    })
                    .Select(p =>
                    {
                        var playerInfo = new PlayerInfo
                        {
                            Name = p.client.Name,
                            ClientId = p.client.ClientId,
                            TimeOnline = (DateTime.UtcNow - p.client.LastConnection).HumanizeForCurrentCulture(),
                            Level = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read)? p.client.Level.ToLocalizedLevelName() : Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName(),
                            LevelInt = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read) ? (int)p.client.Level : 0,
                            Tag = p.client.Tag,
                            IPAddress = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read) ? p.client.IPAddressString : null,
                            NetworkId = HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read) ? p.client.NetworkId : 0,
                            Online = true,
                            LastConnection = p.client.LastConnection,
                            Score = p.client.Score,
                            Kills = p.stats?.MatchData?.Kills ?? 0,
                            Deaths = p.stats?.MatchData?.Deaths ?? 0,
                            Ping = p.client.Ping,
                            ZScore = p.stats?.ZScore
                        };

                        return playerInfo;
                    })
                    .ToList(),
                ChatHistory = HasPermission(WebfrontEntity.ChatMessage, WebfrontPermission.Read) ? server.ChatHistory.ToList() : [],
                Online = !server.Throttled,
                IPAddress = server.ListenAddress,
                ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal()
                    ? _manager.ExternalIPAddress
                    : server.ListenAddress,
                ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                    server.ResolvedIpEndPoint.Address.IsInternal()
                        ? _manager.ExternalIPAddress
                        : server.ListenAddress, server.ListenPort),
                IsZombieServer = server.IsZombieServer(),
                ZombieRoundNumber = server.ZombieRoundNumber,
                RconRoundTripMs = server.LatencyMetrics?.RconRoundTripMs,
                GameLogIngestMs = server.LatencyMetrics?.GameLogIngestMs,
                PerformanceBucket = server.PerformanceCode
            }).ToList();
    }

    public async Task<ServerInfo?> GetServer(string id)
    {
        var server = _manager.GetServers().FirstOrDefault(s => s.Id == id);
        if (server == null)
            return null;

        // Get complete history (saved + live data)
        var clientHistory = await GetClientHistoryAsync(id);

        return new ServerInfo
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
                ClientCounts = clientHistory.ToList()
            },
            Players = server.GetClientsAsList()
                .Select(client => new
                {
                    client,
                    stats = client.GetAdditionalProperty<EFClientStatistics>(StatManager.CLIENT_STATS_KEY)
                })
                .Select(p =>
                {
                    var playerInfo = new PlayerInfo
                    {
                        Name = p.client.Name,
                        ClientId = p.client.ClientId,
                        TimeOnline = (DateTime.UtcNow - p.client.LastConnection).HumanizeForCurrentCulture(),
                        Level = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read)? p.client.Level.ToLocalizedLevelName() : Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName(),
                        LevelInt = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read) ? (int)p.client.Level : 0,
                        Tag = p.client.Tag,
                        IPAddress = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read) ? p.client.IPAddressString : null,
                        NetworkId = HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read) ? p.client.NetworkId : 0,
                        Online = true,
                        LastConnection = p.client.LastConnection,
                        Score = p.client.Score,
                        Kills = p.stats?.MatchData?.Kills ?? 0,
                        Deaths = p.stats?.MatchData?.Deaths ?? 0,
                        Ping = p.client.Ping,
                        ZScore = p.stats?.ZScore
                    };

                   return playerInfo;
                })
                .ToList(),
            ChatHistory = HasPermission(WebfrontEntity.ChatMessage, WebfrontPermission.Read) ? server.ChatHistory.ToList() : [],
            Online = !server.Throttled,
            IPAddress = server.ListenAddress,
            ExternalIPAddress = server.ResolvedIpEndPoint.Address.IsInternal()
                ? _manager.ExternalIPAddress
                : server.ListenAddress,
            ConnectProtocolUrl = server.EventParser.URLProtocolFormat.FormatExt(
                server.ResolvedIpEndPoint.Address.IsInternal() ? _manager.ExternalIPAddress : server.ListenAddress,
                server.ListenPort),
            IsZombieServer = server.IsZombieServer(),
            ZombieRoundNumber = server.ZombieRoundNumber,
            RconRoundTripMs = server.LatencyMetrics?.RconRoundTripMs,
            GameLogIngestMs = server.LatencyMetrics?.GameLogIngestMs
        };
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
            CommandPrefix = _manager.GetApplicationSettings().Configuration().CommandPrefix,
            TotalServerCount = servers.Count
        };
    }

    public async Task<NavigationInfo> GetNavigationDataAsync()
    {
        // Get pages from Manager's page list (key=name, value=location), plus any per-page navbar icon
        var pageList = _manager.GetPageList();
        var pages = pageList.Pages
            .Select(kvp => new Page
            {
                Name = kvp.Key,
                Location = kvp.Value,
                IconId = pageList.PageIcons.TryGetValue(kvp.Key, out var icon) ? icon : null
            })
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

        ClientInfo? user = null;
        var currentUser = await GetExecutorAsync();
        var authorized = currentUser is not null;

        if (authorized)
        {
            user = new ClientInfo
            {
                ClientId = currentUser!.ClientId,
                Name = currentUser.CurrentAlias?.Name ?? _translationLookup["WEBFRONT_UNKNOWN"],
                Level = currentUser.Level,
                Game = currentUser.GameName
            };
        }

        return new NavigationInfo
        {
            User = user,
            Authorized = authorized,
            Pages = pages
                .Select(page => new Page { Name = page.Name, Location = page.Location, IconId = page.IconId }),
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
                SocialAccounts = (_manager.GetApplicationSettings().Configuration().CommunityInformation?.SocialAccounts
                    ?
                    .Select(s => new SocialAccountInfo
                    {
                        Title = s.Title,
                        Url = s.Url,
                        IconId = s.IconId,
                        IconUrl = s.IconUrl
                    }) ?? []).ToArray()
            },
            TotalClientCount = _manager.GetServers().Sum(server => server.ClientNum),
            TotalAdminCount = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read)
                ? _manager.GetServers().Sum(server =>
                    server.GetClientsAsList()
                        .Count(client => client.Level >= Data.Models.Client.EFClient.Permission.Trusted))
                : null,
            TotalReportCount = HasPermission(WebfrontEntity.Penalty, WebfrontPermission.Read)
                ? _manager.GetServers().Sum(server =>
                    server.Reports.Count(report =>
                        DateTime.UtcNow - report.ReportedOn <= TimeSpan.FromHours(24)))
                : null,
            TotalFlaggedCount = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read)
                ? _manager.GetServers().Sum(server =>
                    server.GetClientsAsList()
                        .Count(client => client.Level == Data.Models.Client.EFClient.Permission.Flagged))
                : null
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
        var authorized = _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

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
            FirstConnection = client.FirstConnection,
            LastConnection = client.LastConnection,
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
            IPs = client.AliasLink.Children
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
            HasTwoFactor = !string.IsNullOrEmpty(client.TwoFactorSecret)
        };

        var canViewIp = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read);
        var canViewGuid = HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read);
        var canViewLevel = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read);

        if (!canViewLevel)
        {
            clientDto.Level = Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName();
            clientDto.LevelInt = (int)Data.Models.Client.EFClient.Permission.User;
        }

        if (!canViewGuid)
        {
            clientDto.NetworkId = 0;
        }

        var meta = await _metaService.GetRuntimeMeta<InformationResponse>(new ClientPaginationRequest
        {
            ClientId = client.ClientId,
            Before = DateTime.UtcNow,
            RequestPermission = GetRequestingPermission()
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
        
        if (clientDto.ActivePenalty != null)
        {
            clientDto.ActivePenaltyPunisherId = clientDto.ActivePenalty.PunisherId;
            clientDto.ActivePenaltyPunisherName = await _clientService.GetClientNameById(clientDto.ActivePenalty.PunisherId);
        }
        
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
            Level = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read)
                ? clientInfo.Level.ToLocalizedLevelName()
                : Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName(),
            NetworkId = HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read) ? clientInfo.NetworkId : 0,
            GameName = clientInfo.GameName.ToString(),
            Tag = metaResult?.Value,
            FirstConnection = clientInfo.FirstConnection,
            LastConnection = clientInfo.LastConnection,
            TotalConnectionTime = clientInfo.TotalConnectionTime,
            Connections = clientInfo.Connections,
        };
    }

    public async Task<IEnumerable<BaseMetaResponse>> GetClientMetaAsync(ClientMetaRequest request)
    {
        var level = GetRequestingPermission();
        
        var metaRequest = new ClientPaginationRequest
        {
            ClientId = request.ClientId,
            Count = request.Count,
            Offset = request.Offset,
            Before = request.StartAt.HasValue ? DateTime.FromFileTimeUtc(request.StartAt.Value) : DateTime.UtcNow,
            RequestPermission = level,
            IsPrivileged = level >= Data.Models.Client.EFClient.Permission.Trusted
        };

        var config = _manager.GetApplicationSettings().Configuration();

        if (!config.Webfront.PermissionSets.TryGetValue(level.ToString(), out var permissionSet))
        {
            permissionSet = [];
        }

        IEnumerable<IClientMeta>? meta;

        if (request.MetaType is null or MetaType.All)
        {
            meta = await _metaService.GetRuntimeMeta(metaRequest);
        }
        else
        {
            meta = request.MetaType switch
            {
                MetaType.Information => await _metaService.GetRuntimeMeta<InformationResponse>(metaRequest,
                    request.MetaType.Value),
                MetaType.AliasUpdate => permissionSet.HasPermission(WebfrontEntity.MetaAliasUpdate,
                    WebfrontPermission.Read)
                    ? await _metaService.GetRuntimeMeta<UpdatedAliasResponse>(metaRequest, request.MetaType.Value)
                    : new List<IClientMeta>(),
                MetaType.ChatMessage => await _metaService.GetRuntimeMeta<MessageResponse>(metaRequest,
                    request.MetaType.Value),
                MetaType.Penalized => permissionSet.HasPermission(WebfrontEntity.Penalty,
                    WebfrontPermission.Read)
                    ? await _metaService.GetRuntimeMeta<AdministeredPenaltyResponse>(metaRequest,
                        request.MetaType.Value)
                    : new List<IClientMeta>(),
                MetaType.ReceivedPenalty => permissionSet.HasPermission(WebfrontEntity.Penalty,
                    WebfrontPermission.Read)
                    ? await _metaService.GetRuntimeMeta<ReceivedPenaltyResponse>(metaRequest,
                        request.MetaType.Value)
                    : new List<IClientMeta>(),
                MetaType.ConnectionHistory => await _metaService.GetRuntimeMeta<ConnectionHistoryResponse>(metaRequest,
                    request.MetaType.Value),
                MetaType.PermissionLevel => await _metaService.GetRuntimeMeta<PermissionLevelChangedResponse>(
                    metaRequest, request.MetaType.Value),
                _ => await _metaService.GetRuntimeMeta(metaRequest)
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

        var canViewLevel = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read);

        return Task.FromResult<ScoreboardInfo?>(new ScoreboardInfo
        {
            MapName = server.CurrentMap.ToString(),
            ServerName = server.Hostname,
            ServerId = server.Id,
            GameCode = server.GameCode,
            OrderByKey = "Score",
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
                    Team = clientData.client.Team,
                    Level = canViewLevel ? clientData.client.Level : Data.Models.Client.EFClient.Permission.User
                })
                .ToList()
        });
    }

    public async Task<ResourceQueryHelperResult<BanInfo>?> GetBansAsync(BanInfoRequest request)
    {
        var results = await _banQueryHelper.QueryResource(request);

        if (results is null)
        {
            return null;
        }

        var canViewIp = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read);
        var canViewGuid = HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read);

        if (canViewIp && canViewGuid)
        {
            return results;
        }

        foreach (var ban in results.Results)
        {
            if (!canViewIp)
            {
                ban.IPAddress = null;
            }

            if (!canViewGuid)
            {
                ban.NetworkId = 0;
            }

            if (ban.AttachedPenalty != null)
            {
                StripRelatedClientInfo(ban.AttachedPenalty.OffenderInfo, canViewIp, canViewGuid);
                StripRelatedClientInfo(ban.AttachedPenalty.PunisherInfo, canViewIp, canViewGuid);
            }

            foreach (var associated in ban.AssociatedPenalties)
            {
                StripRelatedClientInfo(associated.OffenderInfo, canViewIp, canViewGuid);
                StripRelatedClientInfo(associated.PunisherInfo, canViewIp, canViewGuid);
            }
        }

        return results;
    }

    private static void StripRelatedClientInfo(RelatedClientInfo info, bool canViewIp, bool canViewGuid)
    {
        if (!canViewIp)
        {
            info.IPAddress = null;
        }

        if (!canViewGuid)
        {
            info.NetworkId = 0;
        }
    }

    public async Task<IList<AuditInfo>> GetAuditLogAsync(AuditFilterRequest request)
    {
        var auditItems = await _auditRepository.ListAuditInformation(request);
        var canViewIp = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read);

        if (canViewIp)
        {
            return auditItems;
        }

        foreach (var item in auditItems)
        {
            item.OriginIPAddress = null;
        }

        return auditItems;
    }

    public async Task<AuditStatistics> GetAuditStatisticsAsync(AuditFilterRequest request)
    {
        return await _auditRepository.GetStatisticsAsync(request);
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
            return [new CommandResponseInfo { Response = _translationLookup["WEBFRONT_RESPONSE_SERVER_NOT_FOUND"] }];
        }

        var (_, response) = await _remoteCommandService.ExecuteWithResult(client.ClientId, null, command,
            [], server);

        return response.ToList();
    }

    public async Task<IList<PenaltyInfo>> GetPenaltiesAsync(PenaltyRequest request)
    {
        var penalties = await _manager.GetPenaltyService()
            .GetRecentPenalties(request.Count, request.Offset, request.ShowOnly, request.IgnoreAutomated);
        var permission = GetRequestingPermission();
        var filteredPenalties = permission == Data.Models.Client.EFClient.Permission.User
            ? penalties.Where(p => !p.Sensitive).ToList()
            : penalties;

        var canViewIp = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read);
        var canViewGuid = HasPermission(WebfrontEntity.ClientGuid, WebfrontPermission.Read);
        var canViewLevel = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read);

        return filteredPenalties.Select(p =>
        {
            if (!canViewIp)
            {
                p.OffenderIPAddress = null;
                p.PunisherIPAddress = null;
            }

            if (!canViewGuid)
            {
                p.OffenderNetworkId = 0;
                p.PunisherNetworkId = 0;
            }

            if (canViewLevel)
            {
                return p;
            }

            p.OffenderLevel = Data.Models.Client.EFClient.Permission.User;
            p.PunisherLevel = Data.Models.Client.EFClient.Permission.User;

            return p;
        }).ToList();
    }

    public async Task<long> GetPenaltiesCountAsync(PenaltyRequest request)
    {
        return await _manager.GetPenaltyService()
            .GetRecentPenaltiesCount(request.ShowOnly, request.IgnoreAutomated);
    }

    public async Task<string> UnbanClientAsync(int clientId, string reason)
    {
        var executor = await GetExecutorAsync();
        if (executor == null)
            return _translationLookup["WEBFRONT_RESPONSE_UNAUTHORIZED"];

        var targetClient = await _clientService.Get(clientId);
        if (targetClient == null)
            return _translationLookup["WEBFRONT_RESPONSE_CLIENT_NOT_FOUND"];

        var server = _manager.GetServers().First();
        executor.CurrentServer = server;

        var unbanEvent = targetClient.Unban(reason, executor);
        await unbanEvent.WaitAsync();

        if (!unbanEvent.Failed)
        {
            return unbanEvent.Output.Count > 0
                ? string.Join(" ", unbanEvent.Output)
                : _translationLookup["WEBFRONT_RESPONSE_UNBANNED_SUCCESS"];
        }

        var msg = unbanEvent.Output.Count > 0
            ? string.Join(" ", unbanEvent.Output)
            : _translationLookup["WEBFRONT_RESPONSE_UNBAN_FAILED"];

        return msg;
    }

    public async Task<TopStatsResponse> GetTopStatsAsync(TopStatsRequest request)
    {
        var server = _manager.GetServers().FirstOrDefault(s => s.Id == request.ServerId) as IGameServer;
        var legacyId = server?.LegacyDatabaseId;

        List<TopStatsInfo> stats;
        int rowsConsumed;
        if (_statsConfig.EnableAdvancedMetrics)
        {
            (stats, rowsConsumed) = await _statManager.GetNewTopStats(
                request.Offset, request.Count, legacyId, request.PerformanceBucketCode);
        }
        else
        {
            // Legacy path doesn't filter past the ranking query, so consumed == returned.
            stats = await _statManager.GetTopStats(request.Offset, request.Count, legacyId);
            rowsConsumed = stats.Count;
        }

        var totalRanked = await _serverDataViewer.RankedClientsCountAsync(legacyId, request.PerformanceBucketCode);

        return new TopStatsResponse
        {
            Players = stats,
            TotalRankedClients = totalRanked,
            NextOffset = request.Offset + rowsConsumed
        };
    }

    public async Task<AdvancedStatsInfo?> GetClientStatisticsAsync(int clientId, string? serverId = null, string? performanceBucketCode = null)
    {
        var hitInfo = (await _advancedStatsHelper.QueryResource(new StatsInfoRequest
        {
            ClientId = clientId,
            ServerEndpoint = serverId,
            PerformanceBucketCode = performanceBucketCode
        }))?.Results?.First();

        if (hitInfo is null)
        {
            return null;
        }

        if (!HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read))
        {
            hitInfo.Level = Data.Models.Client.EFClient.Permission.User;
        }

        var server = _manager.GetServers().FirstOrDefault(s => s.Id == serverId);
        var matchedServerId = server?.LegacyDatabaseId;

        hitInfo.TotalRankedClients = await _serverDataViewer.RankedClientsCountAsync(matchedServerId);

        // Invoke custom stats metrics (e.g. zombie stats) for advanced view
        var customMeta = new Dictionary<int, List<Data.Models.EFMeta>>
        {
            { clientId, new List<Data.Models.EFMeta>() }
        };

        foreach (var customMetricFunc in _manager.CustomStatsMetrics)
        {
            await customMetricFunc(customMeta, matchedServerId, hitInfo.PerformanceBucket, false);
        }

        hitInfo.CustomMetrics = customMeta[clientId];

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
                Game = client.GameName,
                IsMasked = client.Masked,
                HasTwoFactor = !string.IsNullOrEmpty(client.TwoFactorSecret)
            })
            .GroupBy(client => client.Level)
            .ToDictionary(folder => folder.Key, IList<ClientInfo> (folder) => folder.ToList());
        return admins;
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
        if (!HasPermission(WebfrontEntity.Penalty, WebfrontPermission.Read))
        {
            return Task.FromResult<IEnumerable<ServerReportsInfo>>([]);
        }

        var reports = _manager.GetServers()
            .Select(server => new ServerReportsInfo
            {
                Id = server.LegacyDatabaseId,
                Name = server.Hostname,
                Game = (Reference.Game)server.GameName,
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

    public Task<IEnumerable<ServerAdminsInfo>> GetOnlineAdminsAsync()
    {
        var admins = _manager.GetServers()
            .Select(server => new ServerAdminsInfo
            {
                Id = server.LegacyDatabaseId,
                Name = server.Hostname,
                Game = (Reference.Game)server.GameName,
                Admins = server.GetClientsAsList()
                    .Where(client => client.Level > Data.Models.Client.EFClient.Permission.Flagged)
                    .Select(client => new AdminInfo
                    {
                        Name = client.Name,
                        ClientId = client.ClientId,
                        Level = client.Level.ToLocalizedLevelName(),
                        LevelInt = (int)client.Level
                    })
                    .OrderByDescending(a => a.LevelInt)
                    .ToList()
            })
            .Where(s => s.Admins.Any())
            .ToList();
        return Task.FromResult<IEnumerable<ServerAdminsInfo>>(admins);
    }

    public async Task<IEnumerable<ServerFlaggedInfo>> GetOnlineFlaggedAsync()
    {
        var flagged = new List<ServerFlaggedInfo>();

        foreach (var server in _manager.GetServers())
        {
            var flaggedClients = server.GetClientsAsList()
                .Where(client => client.Level == Data.Models.Client.EFClient.Permission.Flagged)
                .ToList();

            if (!flaggedClients.Any())
                continue;

            var flaggedInfos = new List<FlaggedClientInfo>();
            foreach (var client in flaggedClients)
            {
                // Get the flag penalty to retrieve the reason
                var flagPenalty = await _manager.GetPenaltyService()
                    .GetActivePenaltiesAsync(client.AliasLinkId, client.CurrentAliasId,
                        client.NetworkId, client.GameName, client.IPAddress);

                var flag = flagPenalty.FirstOrDefault(p => p.Type == EFPenalty.PenaltyType.Flag);

                flaggedInfos.Add(new FlaggedClientInfo
                {
                    Name = client.Name,
                    ClientId = client.ClientId,
                    Reason = flag?.Offense ?? _translationLookup["WEBFRONT_UNKNOWN"],
                    FlaggedOn = flag?.When ?? DateTime.UtcNow
                });
            }

            flagged.Add(new ServerFlaggedInfo
            {
                Id = server.LegacyDatabaseId,
                Name = server.Hostname,
                Game = (Reference.Game)server.GameName,
                FlaggedClients = flaggedInfos.OrderByDescending(f => f.FlaggedOn).ToList()
            });
        }

        return flagged;
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

        var commands = _manager.Commands
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
                    Syntax = c.Syntax.Split(":").LastOrDefault()?.Trim() ?? "Translation is missing ':'", // TODO: This will break if translation has the ":" removed.
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

    public async Task<ResourceQueryHelperResult<ClientResourceResponse>> SearchClientsAsync(
        ClientResourceRequest request)
    {
        if (!request.HasData)
        {
            return new ResourceQueryHelperResult<ClientResourceResponse> { Results = [] };
        }

        var canViewIp = HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read);
        var canViewLevel = HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read);

        // Filter request parameters if user lacks permission
        if (!canViewIp)
        {
            request.ClientIp = null;
        }

        if (!canViewLevel)
        {
            request.ClientLevel = null;
        }

        var results = await _clientResourceHelper.QueryResource(request);

        results.Results = results.Results.Select(r =>
        {
            if (!canViewIp)
            {
                r.CurrentClientIp = null;
                r.MatchedClientIp = null;
            }

            if (canViewLevel)
            {
                return r;
            }

            r.ClientLevel = Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName();
            r.ClientLevelValue = Data.Models.Client.EFClient.Permission.User;

            return r;
        });

        return results;
    }


    public async Task<List<MessageResponse>> GetChatContextAsync(string serverId,
        long when)
    {
        var whenTime = DateTime.FromFileTimeUtc(when);
        var whenUpper = whenTime.AddMinutes(5);
        var whenLower = whenTime.AddMinutes(-5);

        var level = GetRequestingPermission();
        
        var messages = await _chatQueryHelper.QueryResource(new ChatSearchQuery
        {
            ServerId = serverId,
            SentBefore = whenUpper,
            SentAfter = whenLower,
            RequestPermission =  level,
            IsPrivileged = level > Data.Models.Client.EFClient.Permission.Trusted
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
        var (totalClients, totalRecentClients) =
            await _serverDataViewer.ClientCountsAsync(duration, null, CancellationToken.None);
        var (maxConcurrent, maxConcurrentTime) =
            await _serverDataViewer.MaxConcurrentClientsAsync(overPeriod: duration, token: CancellationToken.None);
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

        var savedHistory =
            (await _serverDataViewer.ClientHistoryAsync(_appConfig.MaxClientHistoryTime, CancellationToken.None))?
            .FirstOrDefault(history => history.ServerId == foundServer.LegacyDatabaseId);

        var clientCountSnapshots = GetCombinedClientHistory(
            savedHistory?.ClientCounts,
            foundServer.ClientHistory.ClientCounts,
            _appConfig.MaxClientHistoryTime);

        return clientCountSnapshots;
    }

    public async Task<InteractionResponse?> GetInteractionAsync(string interactionName,
        Dictionary<string, string>? query = null)
    {
        var interactionData =
            (await _interactionRegistration.GetInteractions(interactionName, token: CancellationToken.None))
            .FirstOrDefault();

        if (interactionData is null)
        {
            return null;
        }

        var executor = await GetExecutorAsync();

        if ((executor?.Level ?? Data.Models.Client.EFClient.Permission.User) < interactionData.MinimumPermission)
        {
            throw new UnauthorizedAccessException("Insufficient permission to execute interaction");
        }

        var result = await _interactionRegistration.ProcessInteraction(interactionName, executor?.ClientId ?? 0,
            meta: query ?? new Dictionary<string, string>(), token: CancellationToken.None);

        return new InteractionResponse
        {
            Title = interactionData.Description ?? interactionData.Name,
            Content = result ?? "",
            InteractionType = interactionData.InteractionType.ToString(),
            DisplayMeta = interactionData.DisplayMeta
        };
    }

    public async Task<ClaimsPrincipal> LoginAsync(ServiceLoginRequest request)
    {
        if (request.ClientId is 0)
        {
            throw new UnauthorizedAccessException("Invalid Client ID");
        }

        var privilegedClient = await _clientService.GetClientForLogin(request.ClientId);

        var tokenData = new TokenIdentifier
        {
            ClientId = request.ClientId,
            Token = request.Password
        };

        var loginSuccess = _manager.TokenAuthenticator.AuthorizeToken(tokenData) ||
                           (await Task.FromResult(Hashing.Hash(request.Password, privilegedClient.PasswordSalt)))[0] ==
                           privilegedClient.Password;

        if (!loginSuccess)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (!string.IsNullOrEmpty(privilegedClient.TwoFactorSecret))
        {
            if (string.IsNullOrEmpty(request.TwoFactorCode) || request.TwoFactorCode == "null")
            {
                throw new UnauthorizedAccessException("2FA_REQUIRED");
            }

            if (!_twoFactorService.Validate(privilegedClient.TwoFactorSecret, request.TwoFactorCode))
            {
                throw new UnauthorizedAccessException("Invalid credentials");
            }
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
            Owner = _manager.Servers.First(),
            Data = request.IpAddress
        });

        _manager.QueueEvent(new LoginEvent
        {
            Source = this,
            LoginSource = LoginEvent.LoginSourceType.Webfront,
            EntityId = request.ClientId.ToString(),
            Identifier = request.IpAddress
        });

        return claimsPrincipal;
    }

    public async Task<ResourceQueryHelperResult<MessageResponse>> SearchMessagesAsync(ChatSearchQuery request)
    {
        return await _chatQueryHelper.QueryResource(request);
    }

    private bool HasPermission(WebfrontEntity entity, WebfrontPermission permission)
    {
        var role = GetRequestingPermission();
        var config = _manager.GetApplicationSettings().Configuration();
        return config.Webfront.PermissionSets.TryGetValue(role.ToString(), out var set) && set.HasPermission(entity, permission);
    }

    private Data.Models.Client.EFClient.Permission GetRequestingPermission()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Data.Models.Client.EFClient.Permission.User;
        }

        var levelClaim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        return Enum.TryParse(levelClaim, out Data.Models.Client.EFClient.Permission result)
            ? result
            : Data.Models.Client.EFClient.Permission.User;
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

    /// <summary>
    /// Combines saved client history with in-memory server history, avoiding duplicates.
    /// </summary>
    private static List<ClientCountSnapshot> GetCombinedClientHistory(
        List<ClientCountSnapshot>? savedHistory,
        List<ClientCountSnapshot>? liveHistory,
        TimeSpan maxHistoryTime)
    {
        var counts = savedHistory?.AsEnumerable() ?? [];

        if (liveHistory is { Count: > 0 })
        {
            var lastSavedTime = savedHistory?.LastOrDefault()?.Time ?? DateTime.MinValue;

            // Union with live data that's newer than the last saved entry
            counts = counts.Union(liveHistory.Where(h => h.Time > lastSavedTime));
        }

        // Filter to max history time window
        return counts
            .Where(h => h.Time >= DateTime.UtcNow - maxHistoryTime)
            .ToList();
    }
    
    public IEnumerable<string> GetAvailableParsers()
    {
        return _manager.AdditionalRConParsers.Select(p => p.Name).ToList();
    }
    
    public async Task<AddServerResponse?> AddServerAsync(AddServerRequest request, CancellationToken token = default)
    {
        // Validate the parser exists
        if (_manager.AdditionalRConParsers.All(p => p.Name != request.RConParserVersion))
        {
            throw new ArgumentException($"Invalid RCon parser version: {request.RConParserVersion}. Use GetAvailableParsers() to see available parsers.");
        }
        
        if (_manager.AdditionalEventParsers.All(p => p.Name != request.EventParserVersion))
        {
            throw new ArgumentException($"Invalid Event parser version: {request.EventParserVersion}. Use GetAvailableParsers() to see available parsers.");
        }
        
        var config = new ServerConfiguration
        {
            IPAddress = request.IPAddress,
            Port = request.Port,
            Password = request.Password,
            RConParserVersion = request.RConParserVersion,
            EventParserVersion = request.EventParserVersion,
            CustomHostname = request.CustomHostname,
            ManualLogPath = request.ManualLogPath,
            ReservedSlotNumber = request.ReservedSlotNumber,
            GameLogServerUrl = !string.IsNullOrEmpty(request.GameLogServerUrl) 
                ? new Uri(request.GameLogServerUrl) 
                : null
        };
        
        var server = await _manager.AddServerAsync(config, request.PersistToConfiguration, token);
        
        if (server == null)
        {
            return null;
        }
        
        return new AddServerResponse
        {
            ServerId = server.Id,
            Hostname = server.Hostname,
            Game = server.GameName.ToString(),
            Persisted = request.PersistToConfiguration
        };
    }
    
    public async Task<bool> RemoveServerAsync(string serverId, bool persist = false, CancellationToken token = default)
    {
        return await _manager.RemoveServerAsync(serverId, persist, token);
    }
    
    public async Task<AnnouncementInfo?> GetActiveAnnouncementAsync(bool globalOnly = false)
    {
        var announcement = await _announcementService.GetActiveAnnouncementAsync(globalOnly);
        return announcement == null ? null : MapToAnnouncementInfo(announcement);
    }

    public async Task<IEnumerable<AnnouncementInfo>> GetAllAnnouncementsAsync()
    {
        var announcements = await _announcementService.GetAllAnnouncementsAsync();
        return announcements.Select(MapToAnnouncementInfo);
    }

    public async Task<AnnouncementInfo> CreateAnnouncementAsync(CreateAnnouncementRequest request, int createdByClientId)
    {
        var announcement = new Data.Models.Misc.EFAnnouncement
        {
            Title = request.Title,
            Content = request.Content,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            IsActive = request.IsActive,
            IsGlobalNotice = request.IsGlobalNotice,
            CreatedByClientId = createdByClientId
        };
        var result = await _announcementService.CreateAnnouncementAsync(announcement);
        return MapToAnnouncementInfo(result);
    }

    public async Task<AnnouncementInfo> UpdateAnnouncementAsync(UpdateAnnouncementRequest request)
    {
        var announcement = new Data.Models.Misc.EFAnnouncement
        {
            AnnouncementId = request.AnnouncementId,
            Title = request.Title,
            Content = request.Content,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            IsActive = request.IsActive,
            IsGlobalNotice = request.IsGlobalNotice
        };
        var result = await _announcementService.UpdateAnnouncementAsync(announcement);
        return MapToAnnouncementInfo(result);
    }

    public async Task DeleteAnnouncementAsync(int id)
    {
        await _announcementService.DeleteAnnouncementAsync(id);
    }

    public async Task ActivateAnnouncementAsync(int id)
    {
        await _announcementService.ActivateAnnouncementAsync(id);
    }

    public async Task DeactivateAnnouncementAsync(int id)
    {
        await _announcementService.DeactivateAnnouncementAsync(id);
    }

    public async Task<TwoFactorSetupInfo> EnableTwoFactorAsync()
    {
        var executor = await GetExecutorAsync();
        if (executor == null) throw new UnauthorizedAccessException();

        var (secret, qrCodeUrl, manualEntryKey) = _twoFactorService.GenerateSetup(executor.Name);
        return new TwoFactorSetupInfo
        {
            Secret = secret,
            QrCodeUrl = qrCodeUrl,
            ManualEntryKey = manualEntryKey
        };
    }

    public async Task<TwoFactorConfirmResponse> ConfirmTwoFactorAsync(string secret, string code)
    {
        var executor = await GetExecutorAsync();
        if (executor == null) throw new UnauthorizedAccessException();

        if (!_twoFactorService.Validate(secret, code))
        {
            return new TwoFactorConfirmResponse { Success = false };
        }

        var client = await _clientService.Get(executor.ClientId);
        client.TwoFactorSecret = secret;
        
        var backupCodes = _twoFactorService.GenerateBackupCodes();
        var hashedBackupCodes = backupCodes.Select(c => Hashing.Hash(c, client.PasswordSalt)[0]).ToList();
        client.TwoFactorBackupCodes = System.Text.Json.JsonSerializer.Serialize(hashedBackupCodes);
        
        await _clientService.Update(client);
        return new TwoFactorConfirmResponse
        {
            Success = true,
            BackupCodes = backupCodes
        };
    }

    public async Task<bool> ValidateTwoFactorCodeAsync(int clientId, string code)
    {
        if (string.IsNullOrEmpty(code) || code == "null")
        {
            return false;
        }

        var client = await _clientService.Get(clientId);
        if (client == null || string.IsNullOrEmpty(client.TwoFactorSecret))
        {
            return false;
        }

        if (_twoFactorService.Validate(client.TwoFactorSecret, code))
        {
            return true;
        }

        if (string.IsNullOrEmpty(client.TwoFactorBackupCodes))
        {
            return false;
        }

        try
        {
            var backupCodes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(client.TwoFactorBackupCodes);
            if (backupCodes != null)
            {
                var hashedInput = Hashing.Hash(code, client.PasswordSalt)[0];
                var match = backupCodes.FirstOrDefault(c => c == hashedInput);
                    
                if (match != null)
                {
                    backupCodes.Remove(match);
                    client.TwoFactorBackupCodes = System.Text.Json.JsonSerializer.Serialize(backupCodes);
                    await _clientService.Update(client);
                    return true;
                }
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public async Task DisableTwoFactorAsync(int? clientId = null)
    {
        var executor = await GetExecutorAsync();
        if (executor == null) throw new UnauthorizedAccessException();

        var targetId = clientId ?? executor.ClientId;

        if (targetId != executor.ClientId && executor.Level < Data.Models.Client.EFClient.Permission.Owner)
        {
            throw new UnauthorizedAccessException();
        }

        var client = await _clientService.Get(targetId);
        client.TwoFactorSecret = null;
        client.TwoFactorBackupCodes = null;
        await _clientService.Update(client);
    }

    private static AnnouncementInfo MapToAnnouncementInfo(Data.Models.Misc.EFAnnouncement announcement)
    {
        return new AnnouncementInfo
        {
            AnnouncementId = announcement.AnnouncementId,
            Title = announcement.Title,
            Content = announcement.Content,
            StartAt = announcement.StartAt,
            EndAt = announcement.EndAt,
            IsActive = announcement.IsActive,
            IsGlobalNotice = announcement.IsGlobalNotice,
            CreatedByName = announcement.CreatedByClient?.CurrentAlias?.Name ?? "Unknown",
            CreatedByClientId = announcement.CreatedByClientId,
            CreatedDateTime = announcement.CreatedDateTime,
            UpdatedDateTime = announcement.UpdatedDateTime
        };
    }
}
