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
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Data.Models.Server;
using Microsoft.Extensions.Logging;
using IW4MAdmin.Plugins.Stats.Client.Abstractions;
using Stats.Client.Abstractions;
using Stats.Config;
using EFClient = SharedLibraryCore.Database.Models.EFClient;

namespace IW4MAdmin.Plugins.Stats
{
    public class Plugin : IPlugin
    {
        public string Name => "Simple Stats";

        public float Version => (float)Utilities.GetVersionAsDouble();

        public string Author => "RaidMax";

        public static StatManager Manager { get; private set; }
        public static IManager ServerManager;
        public static IConfigurationHandler<StatsConfiguration> Config { get; private set; }

        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly ITranslationLookup _translationLookup;
        private readonly IMetaService _metaService;
        private readonly IResourceQueryHelper<ChatSearchQuery, MessageResponse> _chatQueryHelper;
        private readonly ILogger<StatManager> _managerLogger;
        private readonly ILogger<Plugin> _logger;
        private readonly List<IClientStatisticCalculator> _statCalculators;
        private readonly IServerDistributionCalculator _serverDistributionCalculator;

        public Plugin(ILogger<Plugin> logger, IConfigurationHandlerFactory configurationHandlerFactory, IDatabaseContextFactory databaseContextFactory,
            ITranslationLookup translationLookup, IMetaService metaService, IResourceQueryHelper<ChatSearchQuery, MessageResponse> chatQueryHelper, ILogger<StatManager> managerLogger, 
            IEnumerable<IClientStatisticCalculator> statCalculators, IServerDistributionCalculator serverDistributionCalculator)
        {
            Config = configurationHandlerFactory.GetConfigurationHandler<StatsConfiguration>("StatsPluginSettings");
            _databaseContextFactory = databaseContextFactory;
            _translationLookup = translationLookup;
            _metaService = metaService;
            _chatQueryHelper = chatQueryHelper;
            _managerLogger = managerLogger;
            _logger = logger;
            _statCalculators = statCalculators.ToList();
            _serverDistributionCalculator = serverDistributionCalculator;
        }

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            switch (E.Type)
            {
                case GameEvent.EventType.Start:
                    Manager.AddServer(S);
                    break;
                case GameEvent.EventType.Disconnect:
                    await Manager.RemovePlayer(E.Origin);
                    break;
                case GameEvent.EventType.Say:
                    if (!string.IsNullOrEmpty(E.Data) &&
                        E.Origin.ClientId > 1)
                    {
                        await Manager.AddMessageAsync(E.Origin.ClientId, StatManager.GetIdForServer(S), true, E.Data);
                    }
                    break;
                case GameEvent.EventType.MapChange:
                    Manager.SetTeamBased(StatManager.GetIdForServer(S), S.Gametype != "dm");
                    Manager.ResetKillstreaks(S);
                    await Manager.Sync(S);
                    break;
                case GameEvent.EventType.MapEnd:
                    Manager.ResetKillstreaks(S);
                    await Manager.Sync(S);
                    break;
                case GameEvent.EventType.Command:
                    var shouldPersist = !string.IsNullOrEmpty(E.Data) &&
                                        E.Extra?.GetType().Name == "SayCommand";
                    if (shouldPersist)
                    {
                        await Manager.AddMessageAsync(E.Origin.ClientId, StatManager.GetIdForServer(S), false, E.Data);
                    }
                    break;
                case GameEvent.EventType.ScriptKill:
                    string[] killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if ((S.CustomCallback || ShouldOverrideAnticheatSetting(S)) && killInfo.Length >= 18 && !ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

                        await EnsureClientsAdded(E.Origin, E.Target);
                        await Manager.AddScriptHit(false, E.Time, E.Origin, E.Target, StatManager.GetIdForServer(S), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13], killInfo[14], killInfo[15], killInfo[16], killInfo[17]);
                    }

                    else
                    {
                        _logger.LogDebug("Skipping script kill as it is ignored or data in customcallbacks is outdated/missing");
                    }
                    break;
                case GameEvent.EventType.Kill:
                    if (!ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

                        await EnsureClientsAdded(E.Origin, E.Target);
                        await Manager.AddStandardKill(E.Origin, E.Target);
                    }
                    break;
                case GameEvent.EventType.Damage:
                    if (!ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

                        Manager.AddDamageEvent(E.Data, E.Origin.ClientId, E.Target.ClientId, StatManager.GetIdForServer(S));
                    }
                    break;
                case GameEvent.EventType.ScriptDamage:
                    killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if ((S.CustomCallback || ShouldOverrideAnticheatSetting(S)) && killInfo.Length >= 18 && !ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

                        await EnsureClientsAdded(E.Origin, E.Target);
                        await Manager.AddScriptHit(true, E.Time, E.Origin, E.Target, StatManager.GetIdForServer(S), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13], killInfo[14], killInfo[15], killInfo[16], killInfo[17]);
                    }

                    else
                    {
                        _logger.LogDebug("Skipping script damage as it is ignored or data in customcallbacks is outdated/missing");
                    }
                    break;
            }

            if (!Config.Configuration().EnableAdvancedMetrics)
            {
                return;
            }
            
            foreach (var calculator in _statCalculators)
            {
                await calculator.CalculateForEvent(E);
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
            await Config.BuildAsync();
            // load custom configuration
            if (Config.Configuration() == null)
            {
                Config.Set((StatsConfiguration)new StatsConfiguration().Generate());
            }
            Config.Configuration().ApplyMigration();
            await Config.Save();

            // register the topstats page
            // todo:generate the URL/Location instead of hardcoding
            manager.GetPageList()
                .Pages.Add(
                    Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_TEXT"],
                   "/Stats/TopPlayersAsync");

            // meta data info
            async Task<IEnumerable<InformationResponse>> getStats(ClientPaginationRequest request)
            {
                IList<EFClientStatistics> clientStats;
                await using var ctx = _databaseContextFactory.CreateContext(enableTracking: false);
                clientStats = await ctx.Set<EFClientStatistics>().Where(c => c.ClientId == request.ClientId).ToListAsync();

                int kills = clientStats.Sum(c => c.Kills);
                int deaths = clientStats.Sum(c => c.Deaths);
                double kdr = Math.Round(kills / (double)deaths, 2);
                var validPerformanceValues = clientStats.Where(c => c.Performance > 0);
                int performancePlayTime = validPerformanceValues.Sum(s => s.TimePlayed);
                double performance = Math.Round(validPerformanceValues.Sum(c => c.Performance * c.TimePlayed / performancePlayTime), 2);
                double spm = Math.Round(clientStats.Sum(c => c.SPM) / clientStats.Where(c => c.SPM > 0).Count(), 1);

                return new List<InformationResponse>()
                {
                    new InformationResponse()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_RANKING"],
                        Value = "#" + (await Manager.GetClientOverallRanking(request.ClientId)).ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 0,
                        Type = MetaType.Information
                    },
                    new InformationResponse()
                    {
                           Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KILLS"],
                           Value = kills.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                           Column = 0,
                           Order = 1,
                           Type = MetaType.Information
                    },
                    new InformationResponse()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_DEATHS"],
                        Value = deaths.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 2,
                        Type = MetaType.Information
                    },
                    new InformationResponse()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KDR"],
                        Value = kdr.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 3,
                        Type = MetaType.Information
                    },
                    new InformationResponse()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_PERFORMANCE"],
                        Value = performance.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 4,
                        Type = MetaType.Information
                    },
                    new InformationResponse()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_META_SPM"],
                        Value = spm.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 5,
                        Type = MetaType.Information
                    }
                };
            }

            async Task<IEnumerable<InformationResponse>> getAnticheatInfo(ClientPaginationRequest request)
            {
                IList<EFClientStatistics> clientStats;

                await using var ctx = _databaseContextFactory.CreateContext(enableTracking: false);
                clientStats = await ctx.Set<EFClientStatistics>()
                    .Include(c => c.HitLocations)
                    .Where(c => c.ClientId == request.ClientId)
                    .ToListAsync();

                double headRatio = 0;
                double chestRatio = 0;
                double abdomenRatio = 0;
                double chestAbdomenRatio = 0;
                double hitOffsetAverage = 0;
                double averageSnapValue = 0;
                double maxStrain = clientStats.Count(c => c.MaxStrain > 0) == 0 ? 0 : clientStats.Max(cs => cs.MaxStrain);

                if (clientStats.Where(cs => cs.HitLocations.Count > 0).FirstOrDefault() != null)
                {
                    chestRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                    c.HitLocations.First(hl => hl.Location == (int)IW4Info.HitLocation.torso_upper).HitCount) /
                    (double)clientStats.Where(c => c.HitLocations.Count > 0)
                    .Sum(c => c.HitLocations.Where(hl => hl.Location != (int)IW4Info.HitLocation.none).Sum(f => f.HitCount))) * 100.0, 0);

                    abdomenRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                         c.HitLocations.First(hl => hl.Location == (int)IW4Info.HitLocation.torso_lower).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(c => c.HitLocations.Where(hl => hl.Location != (int)IW4Info.HitLocation.none).Sum(f => f.HitCount))) * 100.0, 0);

                    chestAbdomenRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == (int)IW4Info.HitLocation.torso_upper).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == (int)IW4Info.HitLocation.torso_lower).HitCount)) * 100.0, 0);

                    headRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == (int)IW4Info.HitLocation.head).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0)
                            .Sum(c => c.HitLocations.Where(hl => hl.Location != (int)IW4Info.HitLocation.none).Sum(f => f.HitCount))) * 100.0, 0);

                    var validOffsets = clientStats.Where(c => c.HitLocations.Count(hl => hl.HitCount > 0) > 0).SelectMany(hl => hl.HitLocations);
                    hitOffsetAverage = validOffsets.Sum(o => o.HitCount * o.HitOffsetAverage) / (double)validOffsets.Sum(o => o.HitCount);
                    averageSnapValue = clientStats.Any(_stats => _stats.AverageSnapValue > 0) ? clientStats.Where(_stats => _stats.AverageSnapValue > 0).Average(_stat => _stat.AverageSnapValue) : 0;
                }

                return new List<InformationResponse>()
                {
                    new InformationResponse()
                    {
                        Key =  $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 1",
                        Value = chestRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 0,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM1"],
                        IsSensitive = true
                    },
                    new InformationResponse()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 2",
                        Value = abdomenRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 1,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM2"],
                        IsSensitive = true
                    },
                    new InformationResponse()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 3",
                        Value = chestAbdomenRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 2,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM3"],
                        IsSensitive = true
                    },
                    new InformationResponse()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 4",
                        Value = headRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 3,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM4"],
                        IsSensitive = true
                    },
                    new InformationResponse()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 5",
                        // todo: make sure this is wrapped somewhere else
                        Value = $"{Math.Round(((float)hitOffsetAverage), 4).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName))}°",
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 4,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM5"],
                        IsSensitive = true
                    },
                    new InformationResponse()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 6",
                        Value = Math.Round(maxStrain, 3).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 5,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM6"],
                        IsSensitive = true
                    },
                    new InformationResponse()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 7",
                        Value = Math.Round(averageSnapValue, 3).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = MetaType.Information,
                        Column = 2,
                        Order = 6,
                        ToolTipText = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM7"],
                        IsSensitive = true
                    }
                };
            }

            async Task<IEnumerable<MessageResponse>> getMessages(ClientPaginationRequest request)
            {
                var query = new ChatSearchQuery()
                {
                    ClientId = request.ClientId,
                    Before = request.Before,
                    SentBefore = request.Before ?? DateTime.UtcNow,
                    Count = request.Count,
                    IsProfileMeta = true
                };

                return (await _chatQueryHelper.QueryResource(query)).Results;
            }

            if (Config.Configuration().AnticheatConfiguration.Enable)
            {
                _metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information, getAnticheatInfo);
            }

            _metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information, getStats);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, MessageResponse>(MetaType.ChatMessage, getMessages);

            async Task<string> totalKills(Server server)
            {
                await using var context = _databaseContextFactory.CreateContext(false);
                long kills = await context.Set<EFServerStatistics>().Where(s => s.Active).SumAsync(s => s.TotalKills);
                return kills.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName));
            }

            async Task<string> totalPlayTime(Server server)
            {
                await using var context = _databaseContextFactory.CreateContext(false);
                long playTime = await context.Set<EFServerStatistics>().Where(s => s.Active).SumAsync(s => s.TotalPlayTime);
                return (playTime / 3600.0).ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName));
            }

            async Task<string> topStats(Server s)
            {
                // todo: this needs to needs to be updated when we DI the lookup
                return string.Join(Environment.NewLine, await Commands.TopStats.GetTopStats(s, Utilities.CurrentLocalization.LocalizationIndex));
            }

            async Task<string> mostPlayed(Server s)
            {
                // todo: this needs to needs to be updated when we DI the lookup
                return string.Join(Environment.NewLine, await Commands.MostPlayedCommand.GetMostPlayed(s, Utilities.CurrentLocalization.LocalizationIndex, _databaseContextFactory));
            }

            async Task<string> mostKills(Server gameServer)
            {
                return string.Join(Environment.NewLine,
                    await Commands.MostKillsCommand.GetMostKills(StatManager.GetIdForServer(gameServer), Config.Configuration(), _databaseContextFactory, _translationLookup));
            }

            manager.GetMessageTokens().Add(new MessageToken("TOTALKILLS", totalKills));
            manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", totalPlayTime));
            manager.GetMessageTokens().Add(new MessageToken("TOPSTATS", topStats));
            manager.GetMessageTokens().Add(new MessageToken("MOSTPLAYED", mostPlayed));
            manager.GetMessageTokens().Add(new MessageToken("MOSTKILLS", mostKills));

            if (Config.Configuration().EnableAdvancedMetrics)
            {
                foreach (var calculator in _statCalculators)
                {
                    await calculator.GatherDependencies();
                }
            }

            ServerManager = manager;
            Manager = new StatManager(_managerLogger, manager, _databaseContextFactory, Config.Configuration(), _serverDistributionCalculator);
            await _serverDistributionCalculator.Initialize();
        }

        public Task OnTickAsync(Server S)
        {
            return Task.CompletedTask;
        }

        public async Task OnUnloadAsync()
        {
            foreach (var sv in ServerManager.GetServers())
            {
                await Manager.Sync(sv);
            }
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
            return ((origin?.NetworkId == Utilities.WORLD_ID && target?.NetworkId == Utilities.WORLD_ID));
        }

        /// <summary>
        /// Indicates if the damage occurs from world (fall damage/certain killstreaks)
        /// </summary>
        /// <param name="origin"></param>
        /// <returns></returns>
        private bool IsWorldDamage(EFClient origin) => origin?.NetworkId == Utilities.WORLD_ID || origin?.ClientId == Utilities.WORLD_ID;

        /// <summary>
        /// Indicates if we should try to use anticheat even if sv_customcallbacks is not defined
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private bool ShouldOverrideAnticheatSetting(Server s) => Config.Configuration().AnticheatConfiguration.Enable && s.GameName == Server.Game.IW5;

        /// <summary>
        /// Makes sure both clients are added
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private async Task EnsureClientsAdded(EFClient origin, EFClient target)
        {
            await Manager.AddPlayer(origin);

            if (!origin.Equals(target))
            {
                await Manager.AddPlayer(target);
            }
        }
    }
}
