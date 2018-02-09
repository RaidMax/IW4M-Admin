using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary;
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

        private StatManager Manager;

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
                    Manager.AddPlayer(E.Origin);
                    break;
                case Event.GType.Disconnect:
                    await Manager.RemovePlayer(E.Origin);
                    break;
                case Event.GType.Say:
                    break;
                case Event.GType.MapChange:
                    break;
                case Event.GType.MapEnd:
                    await Manager.Sync();
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
                    if (killInfo.Length >= 9 && killInfo[0].Contains("ScriptKill"))
                        await Manager.AddScriptKill(E.Origin, E.Target, S.GetHashCode(), S.CurrentMap.Name, killInfo[7], killInfo[8], killInfo[5], killInfo[6], killInfo[3], killInfo[4]);              
                    break;
                case Event.GType.Death:
                    break;
            }
        }

        public Task OnLoadAsync(IManager manager)
        {
            /*
             * 
            ManagerInstance.GetMessageTokens().Add(new MessageToken("TOTALKILLS", GetTotalKills));
            ManagerInstance.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", GetTotalPlaytime));
*/
            string totalKills()
            {
                var serverStats = new GenericRepository<EFServerStatistics>();
                return serverStats.GetQuery(s => s.Active)
                    .Sum(c => c.TotalKills).ToString();
            }

            string totalPlayTime()
            {
                var serverStats = new GenericRepository<EFServerStatistics>();
                return serverStats.GetQuery(s => s.Active)
                    .Sum(c => c.TotalPlayTime).ToString();
            }

            manager.GetMessageTokens().Add(new MessageToken("TOTALKILLS", totalKills));
            manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", totalPlayTime));
            return Task.FromResult(
                Manager = new StatManager(manager)
            );
        }

        public async Task OnTickAsync(Server S)
        {
            
        }

        public Task OnUnloadAsync()
        {
            return Task.FromResult(
                Manager = null
            );
        }
    }
}
