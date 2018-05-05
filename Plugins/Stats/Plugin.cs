using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;

using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using IW4MAdmin.Plugins.Stats.Config;
using IW4MAdmin.Plugins.Stats.Helpers;
using IW4MAdmin.Plugins.Stats.Models;

namespace IW4MAdmin.Plugins.Stats
{
    class Plugin : IPlugin
    {
        public string Name => "Simple Stats";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        public static StatManager Manager { get; private set; }
        private IManager ServerManager;
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
                    break;
                case GameEvent.EventType.Say:
                    if (E.Data != string.Empty && E.Data.Trim().Length > 0 && E.Message.Trim()[0] != '!' && E.Origin.ClientId > 1)
                        await Manager.AddMessageAsync(E.Origin.ClientId, E.Owner.GetHashCode(), E.Data);
                    break;
                case GameEvent.EventType.MapChange:
                    Manager.ResetKillstreaks(S.GetHashCode());
                    await Manager.Sync(S);
                    break;
                case GameEvent.EventType.MapEnd:
                    break;
                case GameEvent.EventType.Broadcast:
                    break;
                case GameEvent.EventType.Tell:
                    break;
                case GameEvent.EventType.Kick:
                    break;
                case GameEvent.EventType.Ban:
                    break;
                case GameEvent.EventType.Remote:
                    break;
                case GameEvent.EventType.Unknown:
                    break;
                case GameEvent.EventType.Report:
                    break;
                case GameEvent.EventType.Flag:
                    break;
                case GameEvent.EventType.Script:
                    break;
                case GameEvent.EventType.Kill:
                    string[] killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if (killInfo.Length >= 9 && killInfo[0].Contains("ScriptKill") && E.Owner.CustomCallback)
                        await Manager.AddScriptKill(E.Time, E.Origin, E.Target, S.GetHashCode(), S.CurrentMap.Name, killInfo[7], killInfo[8],
                            killInfo[5], killInfo[6], killInfo[3], killInfo[4], killInfo[9], killInfo[10], killInfo[11], killInfo[12], killInfo[13]);
                    else if (!E.Owner.CustomCallback)
                        await Manager.AddStandardKill(E.Origin, E.Target);
                    break;
                case GameEvent.EventType.Death:
                    break;
                case GameEvent.EventType.Damage:
                    Manager.AddDamageEvent(E.Data, E.Origin.ClientId, E.Owner.GetHashCode());
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

            // meta data info
            async Task<List<ProfileMeta>> getStats(int clientId)
            {
                var statsSvc = new GenericRepository<EFClientStatistics>();
                var clientStats = await statsSvc.FindAsync(c => c.ClientId == clientId);

                int kills = clientStats.Sum(c => c.Kills);
                int deaths = clientStats.Sum(c => c.Deaths);
                double kdr = Math.Round(kills / (double)deaths, 2);
                double skill = Math.Round(clientStats.Sum(c => c.Skill) / clientStats.Where(c => c.Skill > 0).Count(), 2);
                double spm = Math.Round(clientStats.Sum(c => c.SPM) / clientStats.Where(c => c.SPM > 0).Count(), 1);

                return new List<ProfileMeta>()
                {
                    new ProfileMeta()
                    {
                           Key = "Kills",
                           Value = kills
                    },
                    new ProfileMeta()
                    {
                        Key = "Deaths",
                        Value = deaths
                    },
                    new ProfileMeta()
                    {
                        Key = "KDR",
                        Value = kdr
                    },
                    new ProfileMeta()
                    {
                        Key = "Skill",
                        Value = skill
                    },
                    new ProfileMeta()
                    {
                        Key = "Score Per Minute",
                        Value = spm
                    }
                };
            }

            async Task<List<ProfileMeta>> getAnticheatInfo(int clientId)
            {
                var statsSvc = new GenericRepository<EFClientStatistics>();
                var clientStats = await statsSvc.FindAsync(c => c.ClientId == clientId);

                double headRatio = 0;
                double chestRatio = 0;
                double abdomenRatio = 0;
                double chestAbdomenRatio = 0;
                double hitOffsetAverage = 0;
                double maxStrain = clientStats.Count(c=> c.MaxStrain > 0) == 0 ? 0 : clientStats.Max(cs => cs.MaxStrain);
                //double maxAngle = clientStats.Max(cs => cs.HitLocations.Max(hl => hl.MaxAngleDistance));

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
                        Key = "Chest Ratio",
                        Value = chestRatio,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = "Abdomen Ratio",
                        Value = abdomenRatio,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = "Chest To Abdomen Ratio",
                        Value = chestAbdomenRatio,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = "Headshot Ratio",
                        Value = headRatio,
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = "Hit Offset Average",
                        Value = $"{Math.Round(((float)hitOffsetAverage), 4)}°",
                        Sensitive = true
                    },
                    new ProfileMeta()
                    {
                        Key = "Max Strain",
                        Value = Math.Round(maxStrain, 3),
                        Sensitive = true
                    },
                    /*new ProfileMeta()
                    {
                        Key = "Max Angle Distance",
                        Value = Math.Round(maxAngle, 1),
                        Sensitive = true
                    }*/
                };
            }

            async Task<List<ProfileMeta>> getMessages(int clientId)
            {
                var messageSvc = new GenericRepository<EFClientMessage>();
                var messages = await messageSvc.FindAsync(m => m.ClientId == clientId);
                var messageMeta = messages.Select(m => new ProfileMeta()
                {
                    Key = "EventMessage",
                    Value = m.Message,
                    When = m.TimeSent
                }).ToList();
                messageMeta.Add(new ProfileMeta()
                {
                    Key = "Messages",
                    Value = messages.Count
                });

                return messageMeta;
            }

            MetaService.AddMeta(getStats);

            if (Config.Configuration().EnableAntiCheat)
            {
                MetaService.AddMeta(getAnticheatInfo);
            }

            MetaService.AddMeta(getMessages);

            string totalKills(Server server)
            {
                var serverStats = new GenericRepository<EFServerStatistics>();
                return serverStats.Find(s => s.Active)
                    .Sum(c => c.TotalKills).ToString("#,##0");
            }

            string totalPlayTime(Server server)
            {
                var serverStats = new GenericRepository<EFServerStatistics>();
                return Math.Ceiling((serverStats.GetQuery(s => s.Active)
                    .Sum(c => c.TotalPlayTime) / 3600.0)).ToString("#,##0");
            }

            string topStats(Server s)
            {
                return String.Join(Environment.NewLine, Commands.TopStats.GetTopStats(s).Result);
            }

            manager.GetMessageTokens().Add(new MessageToken("TOTALKILLS", totalKills));
            manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", totalPlayTime));
            manager.GetMessageTokens().Add(new MessageToken("TOPSTATS", topStats));

            ServerManager = manager;

            Manager = new StatManager(manager);
        }

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public async Task OnUnloadAsync()
        {
            foreach (var sv in ServerManager.GetServers())
                await Manager.Sync(sv);
        }
    }
}
