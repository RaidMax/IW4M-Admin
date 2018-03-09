using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Dtos;
using SharedLibrary.Helpers;
using SharedLibrary.Interfaces;
using SharedLibrary.Services;
using StatsPlugin.Helpers;
using StatsPlugin.Models;

namespace StatsPlugin
{
    class Plugin : IPlugin
    {
        public string Name => "Simple Stats";

        public float Version => 1.0f;

        public string Author => "RaidMax";

        public static StatManager Manager { get; private set; }
        private IManager ServerManager;

        public async Task OnEventAsync(Event E, Server S)
        {
            switch (E.Type)
            {
                case Event.GType.Start:
                    Manager.AddServer(S);
                    break;
                case Event.GType.Stop:
                    break;
                case Event.GType.Connect:
                    await Manager.AddPlayer(E.Origin);
                    break;
                case Event.GType.Disconnect:
                    await Manager.RemovePlayer(E.Origin);
                    break;
                case Event.GType.Say:
                    if (E.Data != string.Empty && E.Data.Trim().Length > 0 && E.Message.Trim()[0] != '!' && E.Origin.ClientId > 1)
                        await Manager.AddMessageAsync(E.Origin.ClientId, E.Owner.GetHashCode(), E.Data);
                    break;
                case Event.GType.MapChange:
                    Manager.ResetKillstreaks(S.GetHashCode());
                    await Manager.Sync(S);
                    break;
                case Event.GType.MapEnd:
                    break;
                case Event.GType.Broadcast:
                    break;
                case Event.GType.Tell:
                    break;
                case Event.GType.Kick:
                    break;
                case Event.GType.Ban:
                    break;
                case Event.GType.Remote:
                    break;
                case Event.GType.Unknown:
                    break;
                case Event.GType.Report:
                    break;
                case Event.GType.Flag:
                    break;
                case Event.GType.Script:
                    break;
                case Event.GType.Kill:
                    string[] killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];
                    if (killInfo.Length >= 9 && killInfo[0].Contains("ScriptKill") && E.Owner.CustomCallback)
                        await Manager.AddScriptKill(E.Origin, E.Target, S.GetHashCode(), S.CurrentMap.Name, killInfo[7], killInfo[8], killInfo[5], killInfo[6], killInfo[3], killInfo[4]);
                    else if (!E.Owner.CustomCallback)
                        await Manager.AddStandardKill(E.Origin, E.Target);
                    break;
                case Event.GType.Death:
                    break;
            }
        }

        public Task OnLoadAsync(IManager manager)
        {
            // meta data info
            async Task<List<ProfileMeta>> getStats(int clientId)
            {
                var statsSvc = new GenericRepository<EFClientStatistics>();
                var clientStats = await statsSvc.FindAsync(c => c.ClientId == clientId);

                int kills = clientStats.Sum(c => c.Kills);
                int deaths = clientStats.Sum(c => c.Deaths);
                double kdr = Math.Round(kills / (double)deaths, 2);
                double skill = Math.Round(clientStats.Sum(c => c.Skill) / clientStats.Count, 2);

                double chestRatio = 0;
                double abdomenRatio = 0;
                double chestAbdomenRatio = 0;

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
                }

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
                         }
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
            MetaService.AddMeta(getMessages);

            // todo: is this fast? make async?
            string totalKills()
            {
                var serverStats = new GenericRepository<EFServerStatistics>();
                return serverStats.Find(s => s.Active)
                    .Sum(c => c.TotalKills).ToString("#,##0");
            }

            string totalPlayTime()
            {
                var serverStats = new GenericRepository<EFServerStatistics>();
                return Math.Ceiling((serverStats.GetQuery(s => s.Active)
                    .Sum(c => c.TotalPlayTime) / 3600.0)).ToString("#,##0");
            }

            manager.GetMessageTokens().Add(new MessageToken("TOTALKILLS", totalKills));
            manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", totalPlayTime));

            ServerManager = manager;

            return Task.FromResult(
                Manager = new StatManager(manager)
            );
        }

        public async Task OnTickAsync(Server S)
        {

        }

        public async Task OnUnloadAsync()
        {
            foreach (var sv in ServerManager.GetServers())
                await Manager.Sync(sv);
        }
    }
}
