using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Helpers;
using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
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
                    await Manager.Sync(S);
                    break;
                case GameEvent.EventType.Say:
                    if (!string.IsNullOrEmpty(E.Data) &&
                        E.Origin.ClientId > 1)
                    {
                        await Manager.AddMessageAsync(E.Origin.ClientId, await StatManager.GetIdForServer(E.Owner), E.Data);
                    }
                    break;
                case GameEvent.EventType.MapChange:
                    Manager.SetTeamBased(await StatManager.GetIdForServer(E.Owner), E.Owner.Gametype != "dm");
                    Manager.ResetKillstreaks(await StatManager.GetIdForServer(E.Owner));
                    break;
                case GameEvent.EventType.MapEnd:
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
                    if (killInfo.Length >= 14)
                    {
                        if (E.Origin.ClientId <= 1 && E.Target.ClientId <= 1)
                        {
                            return;
                        }

                        // this treats "world" damage as self damage
                        if (E.Origin.ClientId <= 1)
                        {
                            E.Origin = E.Target;
                        }

                        if (E.Target.ClientId <= 1)
                        {
                            E.Target = E.Origin;
                        }

                        await Manager.AddScriptHit(false, E.Time, E.Origin, E.Target, await StatManager.GetIdForServer(E.Owner), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13], killInfo[14], killInfo[15]);
                    }
                    break;
                case GameEvent.EventType.Kill:
                    if (!E.Owner.CustomCallback)
                    {
                        if (E.Origin.ClientId <= 1 && E.Target.ClientId <= 1)
                        {
                            return;
                        }

                        // this treats "world" damage as self damage
                        if (E.Origin.ClientId <= 1)
                        {
                            E.Origin = E.Target;
                        }

                        if (E.Target.ClientId <= 1)
                        {
                            E.Target = E.Origin;
                        }

                        await Manager.AddStandardKill(E.Origin, E.Target);
                    }
                    break;
                case GameEvent.EventType.Damage:
                    if (!E.Owner.CustomCallback)
                    {
                        if (E.Origin.ClientId <= 1 && E.Target.ClientId <= 1)
                        {
                            return;
                        }

                        // this treats "world" damage as self damage
                        if (E.Origin.ClientId <= 1)
                        {
                            E.Origin = E.Target;
                        }

                        if (E.Target.ClientId <= 1)
                        {
                            E.Target = E.Origin;
                        }

                        Manager.AddDamageEvent(E.Data, E.Origin.ClientId, E.Target.ClientId, await StatManager.GetIdForServer(E.Owner));
                    }
                    break;
                case GameEvent.EventType.ScriptDamage:
                    killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if (killInfo.Length >= 14)
                    {
                        if (E.Origin.ClientId <= 1 && E.Target.ClientId <= 1)
                        {
                            return;
                        }

                        // this treats "world" damage as self damage
                        if (E.Origin.ClientId <= 1)
                        {
                            E.Origin = E.Target;
                        }

                        if (E.Target.ClientId <= 1)
                        {
                            E.Target = E.Origin;
                        }

                        await Manager.AddScriptHit(true, E.Time, E.Origin, E.Target, await StatManager.GetIdForServer(E.Owner), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13], killInfo[14], killInfo[15]);
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
                double maxStrain = clientStats.Count(c => c.MaxStrain > 0) == 0 ? 0 : clientStats.Max(cs => cs.MaxStrain);

                if (clientStats.Where(cs => cs.HitLocations.Count > 0).FirstOrDefault() != null)
                {
                    chestRatio = Math.Round(clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                    c.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_upper).HitCount) /
                    (double)clientStats.Where(c => c.HitLocations.Count > 0)
                    .Sum(c => c.HitLocations.Where(hl => hl.Location != IW4Info.HitLocation.none).Sum(f => f.HitCount)), 2);

                    abdomenRatio = Math.Round(clientStats.Where(c => c.HitLocations.Count > 0).Sum(c =>
                         c.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_lower).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(c => c.HitLocations.Where(hl => hl.Location != IW4Info.HitLocation.none).Sum(f => f.HitCount)), 2);

                    chestAbdomenRatio = Math.Round(clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_upper).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.torso_lower).HitCount), 2);

                    headRatio = Math.Round(clientStats.Where(c => c.HitLocations.Count > 0).Sum(cs => cs.HitLocations.First(hl => hl.Location == IW4Info.HitLocation.head).HitCount) /
                         (double)clientStats.Where(c => c.HitLocations.Count > 0)
                            .Sum(c => c.HitLocations.Where(hl => hl.Location != IW4Info.HitLocation.none).Sum(f => f.HitCount)), 2);

                    var validOffsets = clientStats.Where(c => c.HitLocations.Count(hl => hl.HitCount > 0) > 0).SelectMany(hl => hl.HitLocations);
                    hitOffsetAverage = validOffsets.Sum(o => o.HitCount * o.HitOffsetAverage) / (double)validOffsets.Sum(o => o.HitCount);
                }

                return new List<ProfileMeta>()
                {
                    new ProfileMeta()
                    {
                        Key =  $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 1",
                        Value = chestRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 0,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 2",
                        Value = abdomenRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 1,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 3",
                        Value = chestAbdomenRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 2,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 4",
                        Value = headRatio.ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 3,
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
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = $"{Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_AC_METRIC"]} 6",
                        Value = Math.Round(maxStrain, 3).ToString(new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                        Type = ProfileMeta.MetaType.Information,
                        Column = 2,
                        Order = 5,
                        Sensitive = true
                    },
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
                        Value = m.Message,
                        When = m.TimeSent,
                        Extra = m.ServerId.ToString(),
                        Type = ProfileMeta.MetaType.ChatMessage
                    }).ToListAsync();
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
    }
}
