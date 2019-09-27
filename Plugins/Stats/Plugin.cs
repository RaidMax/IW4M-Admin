using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Helpers;
using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats
{
    public class Plugin : IPlugin
    {
        public string Name => "Simple Stats";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        public static StatManager Manager { get; private set; }
        public static IManager ServerManager;
        public static BaseConfigurationHandler<StatsConfiguration> Config { get; private set; }
#if DEBUG
        int scriptDamageCount;
        int scriptKillCount;
#endif

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            switch (E.Type)
            {
                case GameEvent.EventType.Start:
                    Manager.AddServer(S);
                    break;
                case GameEvent.EventType.Stop:
                    break;
                case GameEvent.EventType.Connect:
                    await Manager.AddPlayer(E.Origin);
                    break;
                case GameEvent.EventType.Disconnect:
                    await Manager.RemovePlayer(E.Origin);
                    break;
                case GameEvent.EventType.Say:
                    if (!string.IsNullOrEmpty(E.Data) &&
                        E.Origin.ClientId > 1)
                    {
                        await Manager.AddMessageAsync(E.Origin.ClientId, StatManager.GetIdForServer(E.Owner), E.Data);
                    }
                    break;
                case GameEvent.EventType.MapChange:
                    Manager.SetTeamBased(StatManager.GetIdForServer(E.Owner), E.Owner.Gametype != "dm");
                    Manager.ResetKillstreaks(StatManager.GetIdForServer(E.Owner));
                    await Manager.Sync(E.Owner);
                    break;
                case GameEvent.EventType.MapEnd:
                    await Manager.Sync(E.Owner);
                    break;
                case GameEvent.EventType.JoinTeam:
                    break;
                case GameEvent.EventType.Broadcast:
                    break;
                case GameEvent.EventType.Tell:
                    break;
                case GameEvent.EventType.Kick:
                    break;
                case GameEvent.EventType.Ban:
                    break;
                case GameEvent.EventType.Unknown:
                    break;
                case GameEvent.EventType.Report:
                    break;
                case GameEvent.EventType.Flag:
                    break;
                case GameEvent.EventType.ScriptKill:
                    string[] killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if (E.Owner.CustomCallback && killInfo.Length >= 14 && !ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

#if DEBUG
                        scriptKillCount++;
                        S.Logger.WriteInfo($"Start ScriptKill {scriptKillCount}");
#endif

                        await Manager.AddScriptHit(false, E.Time, E.Origin, E.Target, StatManager.GetIdForServer(E.Owner), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13], killInfo[14], killInfo[15]);

#if DEBUG
                        S.Logger.WriteInfo($"End ScriptKill {scriptKillCount}");
#endif
                    }
                    break;
                case GameEvent.EventType.Kill:
                    if (!E.Owner.CustomCallback && !ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

                        await Manager.AddStandardKill(E.Origin, E.Target);
                    }
                    break;
                case GameEvent.EventType.Damage:
                    if (!E.Owner.CustomCallback && !ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

                        Manager.AddDamageEvent(E.Data, E.Origin.ClientId, E.Target.ClientId, StatManager.GetIdForServer(E.Owner));
                    }
                    break;
                case GameEvent.EventType.ScriptDamage:
                    killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if (E.Owner.CustomCallback && killInfo.Length >= 14 && !ShouldIgnoreEvent(E.Origin, E.Target))
                    {
                        // this treats "world" damage as self damage
                        if (IsWorldDamage(E.Origin))
                        {
                            E.Origin = E.Target;
                        }

#if DEBUG
                        scriptDamageCount++;
                        S.Logger.WriteInfo($"Start ScriptDamage {scriptDamageCount}");
#endif

                        await Manager.AddScriptHit(true, E.Time, E.Origin, E.Target, StatManager.GetIdForServer(E.Owner), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13], killInfo[14], killInfo[15]);

#if DEBUG
                        S.Logger.WriteInfo($"End ScriptDamage {scriptDamageCount}");
#endif
                    }
                    break;
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
            // load custom configuration
            Config = new BaseConfigurationHandler<StatsConfiguration>("StatsPluginSettings");
            if (Config.Configuration() == null)
            {
                Config.Set((StatsConfiguration)new StatsConfiguration().Generate());
                await Config.Save();
            }

            // register the topstats page
            // todo:generate the URL/Location instead of hardcoding
            manager.GetPageList()
                .Pages.Add(
                    Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_TOP_TEXT"],
                   "/Stats/TopPlayersAsync");

            // meta data info
            async Task<List<ProfileMeta>> getStats(int clientId, int offset, int count, DateTime? startAt)
            {
                if (count > 1)
                {
                    return new List<ProfileMeta>();
                }

                IList<EFClientStatistics> clientStats;
                using (var ctx = new DatabaseContext(disableTracking: true))
                {
                    clientStats = await ctx.Set<EFClientStatistics>().Where(c => c.ClientId == clientId).ToListAsync();
                }

                int kills = clientStats.Sum(c => c.Kills);
                int deaths = clientStats.Sum(c => c.Deaths);
                double kdr = Math.Round(kills / (double)deaths, 2);
                var validPerformanceValues = clientStats.Where(c => c.Performance > 0);
                int performancePlayTime = validPerformanceValues.Sum(s => s.TimePlayed);
                double performance = Math.Round(validPerformanceValues.Sum(c => c.Performance * c.TimePlayed / performancePlayTime), 2);
                double spm = Math.Round(clientStats.Sum(c => c.SPM) / clientStats.Where(c => c.SPM > 0).Count(), 1);

                return new List<ProfileMeta>()
                {
                    new ProfileMeta()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_RANKING"],
                        Value = "#" + (await StatManager.GetClientOverallRanking(clientId)).ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 0,
                        Type = ProfileMeta.MetaType.Information
                    },
                    new ProfileMeta()
                    {
                           Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KILLS"],
                           Value = kills.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                           Column = 0,
                           Order = 1,
                           Type = ProfileMeta.MetaType.Information
                    },
                    new ProfileMeta()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_DEATHS"],
                        Value = deaths.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 2,
                        Type = ProfileMeta.MetaType.Information
                    },
                    new ProfileMeta()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_TEXT_KDR"],
                        Value = kdr.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 3,
                        Type = ProfileMeta.MetaType.Information
                    },
                    new ProfileMeta()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_COMMANDS_PERFORMANCE"],
                        Value = performance.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 4,
                        Type = ProfileMeta.MetaType.Information
                    },
                    new ProfileMeta()
                    {
                        Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_STATS_META_SPM"],
                        Value = spm.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Column = 0,
                        Order = 5,
                        Type = ProfileMeta.MetaType.Information
                    }
                };
            }

            async Task<List<ProfileMeta>> getAnticheatInfo(int clientId, int offset, int count, DateTime? startAt)
            {
                if (count > 1)
                {
                    return new List<ProfileMeta>();
                }

                IList<EFClientStatistics> clientStats;

                using (var ctx = new DatabaseContext(disableTracking: true))
                {
                    clientStats = await ctx.Set<EFClientStatistics>()
                        .Include(c => c.HitLocations)
                        .Where(c => c.ClientId == clientId)
                        .ToListAsync();
                }

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
                    c.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_upper).HitCount) /
                    (double)clientStats.Where(c => c.HitLocations.Count > 0)
                    .Sum(c => c.HitLocations.Where(hl => hl.Location != IW4Info.HitLocation.none).Sum(f => f.HitCount))) * 100.0, 0);

                    abdomenRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                         c.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_lower).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(c => c.HitLocations.Where(hl => hl.Location != IW4Info.HitLocation.none).Sum(f => f.HitCount))) * 100.0, 0);

                    chestAbdomenRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_upper).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_lower).HitCount)) * 100.0, 0);

                    headRatio = Math.Round((clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.head).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0)
                            .Sum(c => c.HitLocations.Where(hl => hl.Location != IW4Info.HitLocation.none).Sum(f => f.HitCount))) * 100.0, 0);

                    var validOffsets = clientStats.Where(c => c.HitLocations.Count(hl => hl.HitCount > 0) > 0).SelectMany(hl => hl.HitLocations);
                    hitOffsetAverage = validOffsets.Sum(o => o.HitCount * o.HitOffsetAverage) / (double)validOffsets.Sum(o => o.HitCount);
                    averageSnapValue = clientStats.Any(_stats => _stats.AverageSnapValue > 0) ? clientStats.Where(_stats => _stats.AverageSnapValue > 0).Average(_stat => _stat.AverageSnapValue) : 0;
                }

                return new List<ProfileMeta>()
                {
                    new ProfileMeta()
                    {
                        Key =  $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 1",
                        Value = chestRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 0,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM1"],
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 2",
                        Value = abdomenRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 1,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM2"],
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 3",
                        Value = chestAbdomenRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 2,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM3"],
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 4",
                        Value = headRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)) + '%',
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 3,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM4"],
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 5",
                        // todo: make sure this is wrapped somewhere else
                        Value = $"{Math.Round(((float)hitOffsetAverage), 4).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName))}°",
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 4,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM5"],
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 6",
                        Value = Math.Round(maxStrain, 3).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 5,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM6"],
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 7",
                        Value = Math.Round(averageSnapValue, 3).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 6,
                        Extra = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_TITLE_ACM7"],
                        Sensitive = true
                    }
                };
            }

            async Task<List<ProfileMeta>> getMessages(int clientId, int offset, int count, DateTime? startAt)
            {
                if (count <= 1)
                {
                    using (var ctx = new DatabaseContext(true))
                    {
                        return new List<ProfileMeta>
                        {
                            new ProfileMeta()
                            {
                                Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_MESSAGES"],
                                Value = (await ctx.Set<EFClientMessage>()
                                    .CountAsync(_message => _message.ClientId == clientId))
                                    .ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                                Column = 1,
                                Order= 4,
                                Type = ProfileMeta.MetaType.Information
                            }
                        };
                    }
                }

                List<ProfileMeta> messageMeta;
                using (var ctx = new DatabaseContext(disableTracking: true))
                {
                    var messages = ctx.Set<EFClientMessage>()
                        .Where(m => m.ClientId == clientId)
                        .Where(_message => _message.TimeSent < startAt)
                        .OrderByDescending(_message => _message.TimeSent)
                        .Skip(offset)
                        .Take(count);

                    messageMeta = await messages.Select(m => new ProfileMeta()
                    {
                        Key = null,
                        Value = new { m.Message, m.Server.GameName },
                        When = m.TimeSent,
                        Extra = m.ServerId.ToString(),
                        Type = ProfileMeta.MetaType.ChatMessage
                    }).ToListAsync();

                    foreach (var message in messageMeta)
                    {
                        if ((message.Value.Message as string).IsQuickMessage())
                        {
                            try
                            {
                                var quickMessages = ServerManager.GetApplicationSettings().Configuration()
                                    .QuickMessages
                                    .First(_qm => _qm.Game == message.Value.GameName);
                                message.Value = quickMessages.Messages[(message.Value.Message as string).Substring(1)];
                                message.Type = ProfileMeta.MetaType.QuickMessage;
                            }
                            catch
                            {
                                message.Value = message.Value.Message;
                            }
                        }

                        else
                        {
                            message.Value = message.Value.Message;
                        }
                    }

                }

                return messageMeta;
            }

            if (Config.Configuration().EnableAntiCheat)
            {
                MetaService.AddRuntimeMeta(getAnticheatInfo);
            }

            MetaService.AddRuntimeMeta(getStats);
            MetaService.AddRuntimeMeta(getMessages);

            async Task<string> totalKills(Server server)
            {
                using (var ctx = new DatabaseContext(disableTracking: true))
                {
                    long kills = await ctx.Set<EFServerStatistics>().Where(s => s.Active).SumAsync(s => s.TotalKills);
                    return kills.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName));
                }
            }

            async Task<string> totalPlayTime(Server server)
            {
                using (var ctx = new DatabaseContext(disableTracking: true))
                {
                    long playTime = await ctx.Set<EFServerStatistics>().Where(s => s.Active).SumAsync(s => s.TotalPlayTime);
                    return (playTime / 3600.0).ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName));
                }
            }

            async Task<string> topStats(Server s)
            {
                return string.Join(Environment.NewLine, await Commands.TopStats.GetTopStats(s));
            }

            async Task<string> mostPlayed(Server s)
            {
                return string.Join(Environment.NewLine, await Commands.MostPlayed.GetMostPlayed(s));
            }

            manager.GetMessageTokens().Add(new MessageToken("TOTALKILLS", totalKills));
            manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", totalPlayTime));
            manager.GetMessageTokens().Add(new MessageToken("TOPSTATS", topStats));
            manager.GetMessageTokens().Add(new MessageToken("MOSTPLAYED", mostPlayed));

            ServerManager = manager;
            Manager = new StatManager(manager);
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
            return ((origin?.NetworkId <= 1 && target?.NetworkId <= 1) || (origin?.ClientId <= 1 && target?.ClientId <= 1));
        }

        /// <summary>
        /// Indicates if the damage occurs from world (fall damage/certain killstreaks)
        /// </summary>
        /// <param name="origin"></param>
        /// <returns></returns>
        private bool IsWorldDamage(EFClient origin) => origin?.NetworkId == 1;
    }
}
