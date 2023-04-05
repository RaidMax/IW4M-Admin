using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using Stats.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client.Stats;
using Data.Models.Server;
using Microsoft.Extensions.Logging;
using IW4MAdmin.Plugins.Stats.Client.Abstractions;
using IW4MAdmin.Plugins.Stats.Events;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces.Events;
using Stats.Client.Abstractions;
using Stats.Config;
using EFClient = SharedLibraryCore.Database.Models.EFClient;

namespace IW4MAdmin.Plugins.Stats;

public class Plugin : IPluginV2
{
    public string Name => "Simple Stats";
    public string Version => Utilities.GetVersionAsString();
    public string Author => "RaidMax";

    public static IManager ServerManager;

    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly ITranslationLookup _translationLookup;
    private readonly IMetaServiceV2 _metaService;
    private readonly IResourceQueryHelper<ChatSearchQuery, MessageResponse> _chatQueryHelper;
    private readonly ILogger<Plugin> _logger;
    private readonly List<IClientStatisticCalculator> _statCalculators;
    private readonly IServerDistributionCalculator _serverDistributionCalculator;
    private readonly IServerDataViewer _serverDataViewer;
    private readonly StatsConfiguration _statsConfig;
    private readonly StatManager _statManager;

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<StatsConfiguration>("StatsPluginSettings");
        serviceCollection.AddSingleton<StatManager>();
    }

    public Plugin(ILogger<Plugin> logger, IDatabaseContextFactory databaseContextFactory,
        ITranslationLookup translationLookup, IMetaServiceV2 metaService,
        IResourceQueryHelper<ChatSearchQuery, MessageResponse> chatQueryHelper,
        IEnumerable<IClientStatisticCalculator> statCalculators,
        IServerDistributionCalculator serverDistributionCalculator, IServerDataViewer serverDataViewer,
        StatsConfiguration statsConfig, StatManager statManager)
    {
        _databaseContextFactory = databaseContextFactory;
        _translationLookup = translationLookup;
        _metaService = metaService;
        _chatQueryHelper = chatQueryHelper;
        _logger = logger;
        _statCalculators = statCalculators.ToList();
        _serverDistributionCalculator = serverDistributionCalculator;
        _serverDataViewer = serverDataViewer;
        _statsConfig = statsConfig;
        _statManager = statManager;

        IGameServerEventSubscriptions.MonitoringStopped +=
            async (monitorEvent, token) => await _statManager.Sync(monitorEvent.Server, token);
        IManagementEventSubscriptions.ClientStateInitialized += async (clientEvent, token) =>
        {
            if (!_statsConfig.EnableAdvancedMetrics)
            {
                return;
            }

            foreach (var calculator in _statCalculators)
            {
                await calculator.CalculateForEvent(clientEvent);
            }
        };
        IManagementEventSubscriptions.ClientStateDisposed +=
            async (clientEvent, token) =>
            {
                await _statManager.RemovePlayer(clientEvent.Client, token);

                if (!_statsConfig.EnableAdvancedMetrics)
                {
                    return;
                }

                if (clientEvent.Client.ClientId == 0)
                {
                    _logger.LogWarning("No client id for {Client}, so we are not doing any stat calculation",
                        clientEvent.Client.ToString());
                    return;
                }

                foreach (var calculator in _statCalculators)
                {
                    await calculator.CalculateForEvent(clientEvent);
                }
            };
        IGameEventSubscriptions.ClientMessaged += async (messageEvent, token) =>
        {
            if (!string.IsNullOrEmpty(messageEvent.Message) &&
                messageEvent.Client.ClientId > 1)
            {
                await _statManager.AddMessageAsync(messageEvent.Client.ClientId,
                    messageEvent.Server.LegacyDatabaseId, true, messageEvent.Message, token);
            }
        };
        IGameEventSubscriptions.MatchEnded += OnMatchEvent;
        IGameEventSubscriptions.MatchStarted += OnMatchEvent;
        IGameEventSubscriptions.ScriptEventTriggered += OnScriptEvent;
        IGameEventSubscriptions.ClientKilled += OnClientKilled;
        IGameEventSubscriptions.ClientDamaged += OnClientDamaged;
        IManagementEventSubscriptions.ClientCommandExecuted += OnClientCommandExecute;
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private async Task OnClientKilled(ClientKillEvent killEvent, CancellationToken token)
    {
        if (!ShouldIgnoreEvent(killEvent.Attacker, killEvent.Victim))
        {
            // this treats "world" damage as self damage
            if (IsWorldDamage(killEvent.Attacker))
            {
                killEvent.UpdateAttacker(killEvent.Victim);
            }

            await EnsureClientsAdded(killEvent.Attacker, killEvent.Victim);
            await _statManager.AddStandardKill(killEvent.Attacker, killEvent.Victim);

            if (!_statsConfig.EnableAdvancedMetrics)
            {
                return;
            }

            foreach (var calculator in _statCalculators)
            {
                await calculator.CalculateForEvent(killEvent);
            }
        }
    }

    private async Task OnClientDamaged(ClientDamageEvent damageEvent, CancellationToken token)
    {
        if (ShouldIgnoreEvent(damageEvent.Attacker, damageEvent.Victim))
        {
            return;
        }

        if (!_statsConfig.EnableAdvancedMetrics)
        {
            return;
        }

        // this treats "world" damage as self damage
        if (IsWorldDamage(damageEvent.Attacker))
        {
            damageEvent.UpdateAttacker(damageEvent.Victim);
        }

        foreach (var calculator in _statCalculators)
        {
            await calculator.CalculateForEvent(damageEvent);
        }
    }

    private async Task OnScriptEvent(GameScriptEvent scriptEvent, CancellationToken token)
    {
        if (scriptEvent is not AntiCheatDamageEvent antiCheatDamageEvent)
        {
            return;
        }

        var killInfo = scriptEvent.ScriptData?.Split(';') ?? Array.Empty<string>();
        if ((scriptEvent.Server.IsLegacyGameIntegrationEnabled ||
             ShouldOverrideAnticheatSetting(scriptEvent.Server)) && killInfo.Length >= 18 &&
            !ShouldIgnoreEvent(antiCheatDamageEvent.Origin, antiCheatDamageEvent.Target))
        {
            // this treats "world" damage as self damage
            if (IsWorldDamage(antiCheatDamageEvent.Origin))
            {
                antiCheatDamageEvent.Origin = antiCheatDamageEvent.Target;
            }

            await EnsureClientsAdded(antiCheatDamageEvent.Origin, antiCheatDamageEvent.Target);
            await _statManager.AddScriptHit(!antiCheatDamageEvent.IsKill, antiCheatDamageEvent.CreatedAt.DateTime,
                antiCheatDamageEvent.Origin,
                antiCheatDamageEvent.Target,
                antiCheatDamageEvent.Server.LegacyDatabaseId, antiCheatDamageEvent.Server.Map.Name,
                killInfo[7], killInfo[8],
                killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11],
                killInfo[12], killInfo[13], killInfo[14], killInfo[15], killInfo[16], killInfo[17]);
        }
    }

    private async Task OnClientCommandExecute(ClientExecuteCommandEvent commandEvent, CancellationToken token)
    {
        var shouldPersist = !string.IsNullOrEmpty(commandEvent.CommandText) && commandEvent.Command.Name == "say";

        if (shouldPersist)
        {
            await _statManager.AddMessageAsync(commandEvent.Client.ClientId,
                (commandEvent.Client.CurrentServer as IGameServer).LegacyDatabaseId, false,
                commandEvent.CommandText, token);
        }
    }

    private async Task OnMatchEvent(GameEventV2 gameEvent, CancellationToken token)
    {
        _statManager.SetTeamBased(gameEvent.Server.LegacyDatabaseId, gameEvent.Server.Gametype != "dm");
        _statManager.ResetKillstreaks(gameEvent.Server);
        await _statManager.Sync(gameEvent.Server, token);

        if (!_statsConfig.EnableAdvancedMetrics)
        {
            return;
        }

        foreach (var calculator in _statCalculators)
        {
            await calculator.CalculateForEvent(gameEvent);
        }
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        // register the topstats page
        // todo:generate the URL/Location instead of hardcoding
        manager.GetPageList()
            .Pages.Add(
                Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_TEXT"],
                "/Stats/TopPlayers");

        // meta data info
        async Task<IEnumerable<InformationResponse>> GetStats(ClientPaginationRequest request,
            CancellationToken token = default)
        {
            await using var ctx = _databaseContextFactory.CreateContext(enableTracking: false);
            IList<EFClientStatistics> clientStats = await ctx.Set<EFClientStatistics>()
                .Where(c => c.ClientId == request.ClientId).ToListAsync(token);

            var kills = clientStats.Sum(c => c.Kills);
            var deaths = clientStats.Sum(c => c.Deaths);
            var kdr = Math.Round(kills / (double)deaths, 2);
            var validPerformanceValues = clientStats.Where(c => c.Performance > 0).ToList();
            var performancePlayTime = validPerformanceValues.Sum(s => s.TimePlayed);
            var performance =
                Math.Round(validPerformanceValues.Sum(c => c.Performance * c.TimePlayed / performancePlayTime), 2);
            var spm = Math.Round(clientStats.Sum(c => c.SPM) / clientStats.Count(c => c.SPM > 0), 1);
            var overallRanking = await _statManager.GetClientOverallRanking(request.ClientId);

            return new List<InformationResponse>
            {
                new InformationResponse
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_RANKING"],
                    Value = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_RANKING_FORMAT"]
                        .FormatExt(
                            (overallRanking == 0
                                ? "--"
                                : overallRanking.ToString("#,##0",
                                    new System.Globalization.CultureInfo(Utilities.CurrentLocalization
                                        .LocalizationName))),
                            (await _serverDataViewer.RankedClientsCountAsync(token: token)).ToString("#,##0",
                                new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName))
                        ),
                    Column = 0,
                    Order = 0,
                    Type = MetaType.Information
                },
                new InformationResponse
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KILLS"],
                    Value = kills.ToString("#,##0",
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Column = 0,
                    Order = 1,
                    Type = MetaType.Information
                },
                new InformationResponse
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_DEATHS"],
                    Value = deaths.ToString("#,##0",
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Column = 0,
                    Order = 2,
                    Type = MetaType.Information
                },
                new InformationResponse
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KDR"],
                    Value = kdr.ToString(
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Column = 0,
                    Order = 3,
                    Type = MetaType.Information
                },
                new InformationResponse
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_PERFORMANCE"],
                    Value = performance.ToString("#,##0",
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Column = 0,
                    Order = 4,
                    Type = MetaType.Information
                },
                new InformationResponse
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_META_SPM"],
                    Value = spm.ToString(
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Column = 0,
                    Order = 5,
                    Type = MetaType.Information
                }
            };
        }

        async Task<IEnumerable<InformationResponse>> GetAnticheatInfo(ClientPaginationRequest request,
            CancellationToken token = default)
        {
            await using var context = _databaseContextFactory.CreateContext(enableTracking: false);
            IList<EFClientStatistics> clientStats = await context.Set<EFClientStatistics>()
                .Include(c => c.HitLocations)
                .Where(c => c.ClientId == request.ClientId)
                .ToListAsync(token);

            double headRatio = 0;
            double chestRatio = 0;
            double abdomenRatio = 0;
            double chestAbdomenRatio = 0;
            double hitOffsetAverage = 0;
            double averageSnapValue = 0;
            var maxStrain = !clientStats.Any(c => c.MaxStrain > 0) ? 0 : clientStats.Max(cs => cs.MaxStrain);

            if (clientStats.Any(cs => cs.HitLocations.Count > 0))
            {
                chestRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                                             c.HitLocations.First(hl =>
                                                 hl.Location == (int)IW4Info.HitLocation.torso_upper).HitCount) /
                                         (double)clientStats.Where(c => c.HitLocations.Count > 0)
                                             .Sum(c => c.HitLocations
                                                 .Where(hl => hl.Location != (int)IW4Info.HitLocation.none)
                                                 .Sum(f => f.HitCount))) * 100.0, 0);

                abdomenRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                                               c.HitLocations.First(hl =>
                                                   hl.Location == (int)IW4Info.HitLocation.torso_lower).HitCount) /
                                           (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                                               c.HitLocations.Where(hl => hl.Location != (int)IW4Info.HitLocation.none)
                                                   .Sum(f => f.HitCount))) * 100.0, 0);

                chestAbdomenRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs =>
                                                    cs.HitLocations.First(hl =>
                                                        hl.Location == (int)IW4Info.HitLocation.torso_upper).HitCount) /
                                                (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs =>
                                                    cs.HitLocations.First(hl =>
                                                            hl.Location == (int)IW4Info.HitLocation.torso_lower)
                                                        .HitCount)) * 100.0, 0);

                headRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs =>
                                            cs.HitLocations.First(hl => hl.Location == (int)IW4Info.HitLocation.head)
                                                .HitCount) /
                                        (double)clientStats.Where(c => c.HitLocations.Count > 0)
                                            .Sum(c => c.HitLocations
                                                .Where(hl => hl.Location != (int)IW4Info.HitLocation.none)
                                                .Sum(f => f.HitCount))) * 100.0, 0);

                var validOffsets = clientStats.Where(c => c.HitLocations.Count(hl => hl.HitCount > 0) > 0)
                    .SelectMany(hl => hl.HitLocations).ToList();
                hitOffsetAverage = validOffsets.Sum(o => o.HitCount * o.HitOffsetAverage) /
                                   (double)validOffsets.Sum(o => o.HitCount);
                averageSnapValue = clientStats.Any(_stats => _stats.AverageSnapValue > 0)
                    ? clientStats.Where(_stats => _stats.AverageSnapValue > 0).Average(_stat => _stat.AverageSnapValue)
                    : 0;
            }

            return new List<InformationResponse>
            {
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 1",
                    Value = chestRatio.ToString(
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                    Type = MetaType.Information,
                    Order = 100,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM1"],
                    IsSensitive = true
                },
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 2",
                    Value = abdomenRatio.ToString(
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                    Type = MetaType.Information,
                    Order = 101,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM2"],
                    IsSensitive = true
                },
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 3",
                    Value = chestAbdomenRatio.ToString(
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                    Type = MetaType.Information,
                    Order = 102,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM3"],
                    IsSensitive = true
                },
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 4",
                    Value = headRatio.ToString(
                        new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                    Type = MetaType.Information,
                    Order = 103,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM4"],
                    IsSensitive = true
                },
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 5",
                    // todo: make sure this is wrapped somewhere else
                    Value =
                        $"{Math.Round(((float)hitOffsetAverage), 4).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName))}°",
                    Type = MetaType.Information,
                    Order = 104,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM5"],
                    IsSensitive = true
                },
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 6",
                    Value = Math.Round(maxStrain, 3)
                        .ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Type = MetaType.Information,
                    Order = 105,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM6"],
                    IsSensitive = true
                },
                new InformationResponse()
                {
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 7",
                    Value = Math.Round(averageSnapValue, 3)
                        .ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Type = MetaType.Information,
                    Order = 106,
                    ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM7"],
                    IsSensitive = true
                }
            };
        }

        async Task<IEnumerable<MessageResponse>> GetMessages(ClientPaginationRequest request,
            CancellationToken token = default)
        {
            var query = new ChatSearchQuery
            {
                ClientId = request.ClientId,
                Before = request.Before,
                SentBefore = request.Before ?? DateTime.UtcNow,
                Count = request.Count,
                IsProfileMeta = true
            };

            return (await _chatQueryHelper.QueryResource(query)).Results;
        }

        if (_statsConfig.AnticheatConfiguration.Enable)
        {
            _metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information,
                GetAnticheatInfo);
        }

        _metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information, GetStats);
        _metaService.AddRuntimeMeta<ClientPaginationRequest, MessageResponse>(MetaType.ChatMessage, GetMessages);

        async Task<string> TotalKills(Server server)
        {
            await using var context = _databaseContextFactory.CreateContext(false);
            var kills = await context.Set<EFServerStatistics>().Where(s => s.Active).SumAsync(s => s.TotalKills);
            return kills.ToString("#,##0",
                new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName));
        }

        async Task<string> TotalPlayTime(Server server)
        {
            await using var context = _databaseContextFactory.CreateContext(false);
            var playTime = await context.Set<EFServerStatistics>().Where(s => s.Active).SumAsync(s => s.TotalPlayTime);
            return (playTime / 3600.0).ToString("#,##0",
                new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName));
        }

        async Task<string> TopStats(Server s)
        {
            // todo: this needs to needs to be updated when we DI the lookup
            return string.Join(Environment.NewLine,
                await Commands.TopStats.GetTopStats(s, Utilities.CurrentLocalization.LocalizationIndex, _statManager));
        }

        async Task<string> MostPlayed(Server s)
        {
            // todo: this needs to needs to be updated when we DI the lookup
            return string.Join(Environment.NewLine,
                await Commands.MostPlayedCommand.GetMostPlayed(s, Utilities.CurrentLocalization.LocalizationIndex,
                    _databaseContextFactory));
        }

        async Task<string> MostKills(IGameServer gameServer)
        {
            return string.Join(Environment.NewLine,
                await Commands.MostKillsCommand.GetMostKills(gameServer.LegacyDatabaseId, _statsConfig,
                    _databaseContextFactory, _translationLookup));
        }

        manager.GetMessageTokens().Add(new MessageToken("TOTALKILLS", TotalKills));
        manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", TotalPlayTime));
        manager.GetMessageTokens().Add(new MessageToken("TOPSTATS", TopStats));
        manager.GetMessageTokens().Add(new MessageToken("MOSTPLAYED", MostPlayed));
        manager.GetMessageTokens().Add(new MessageToken("MOSTKILLS", MostKills));

        if (_statsConfig.EnableAdvancedMetrics)
        {
            foreach (var calculator in _statCalculators)
            {
                await calculator.GatherDependencies();
            }
        }

        ServerManager = manager;
        await _serverDistributionCalculator.Initialize();
    }

    /// <summary>
    /// Indicates if the event should be ignored 
    /// (If the client id or target id is not a real client or the target/origin is a bot and ignore bots is turned on)
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    private bool ShouldIgnoreEvent(EFClient origin, EFClient target)
    {
        return origin?.NetworkId == Utilities.WORLD_ID && target?.NetworkId == Utilities.WORLD_ID;
    }

    /// <summary>
    /// Indicates if the damage occurs from world (fall damage/certain killstreaks)
    /// </summary>
    /// <param name="origin"></param>
    /// <returns></returns>
    private bool IsWorldDamage(EFClient origin) =>
        origin?.NetworkId == Utilities.WORLD_ID || origin?.ClientId == Utilities.WORLD_ID;

    /// <summary>
    /// Indicates if we should try to use anticheat even if sv_customcallbacks is not defined
    /// </summary>
    /// <param name="gameServer"></param>
    /// <returns></returns>
    private bool ShouldOverrideAnticheatSetting(IGameServer gameServer) => _statsConfig.AnticheatConfiguration.Enable &&
                                                                           gameServer.GameCode == Reference.Game.IW5;

    /// <summary>
    /// Makes sure both clients are added
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    private async Task EnsureClientsAdded(EFClient origin, EFClient target)
    {
        await _statManager.AddPlayer(origin);

        if (!origin.Equals(target))
        {
            await _statManager.AddPlayer(target);
        }
    }
}
