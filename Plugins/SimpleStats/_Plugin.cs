using SharedLibrary;
using SharedLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary.Objects;
using System.Linq;
using StatsPlugin.Models;

namespace StatsPlugin
{

    /// <summary>
    /// Each server runs from the same plugin ( for easier reloading and reduced memory usage ).
    /// So, to have multiple stat tracking, we must store a stat struct for each server
    /// </summary>
    public class Stats : SharedLibrary.Interfaces.IPlugin
    {
        public static SharedLibrary.Interfaces.IManager ManagerInstance;
        public static int MAX_KILLEVENTS = 1000;
        public static Dictionary<int, ServerStatInfo> ServerStats { get; private set; }
        public static SharedLibrary.Services.GenericService<Models.EFClientStatistics> ClientStatsSvc;
        public static SharedLibrary.Services.GenericService<Models.EFServer> ServerSvc;

        public class ServerStatInfo
        {
            public ServerStatInfo()
            {
                KillQueue = new Queue<KillInfo>();
                ServerStartTime = DateTime.Now;
            }

            public DateTime ServerStartTime { get; private set; }
            public DateTime RoundStartTime { get; set; }
            public string Uptime => Utilities.GetTimePassed(ServerStartTime, false);
            public string ElapsedRoundTime => Utilities.GetTimePassed(RoundStartTime);
            private Queue<KillInfo> KillQueue { get; set; }
            public Queue<KillInfo> GetKillQueue() { return KillQueue; }
        }

    

        public string Name => "Basic Stats";

        public float Version => 1.1f;

        public string Author => "RaidMax";

        public async Task OnLoadAsync(SharedLibrary.Interfaces.IManager manager)
        {
            statLists = new List<StatTracking>();
            ServerStats = new Dictionary<int, ServerStatInfo>();
            ManagerInstance = manager;

            WebService.PageList.Add(new StatsPage());
            WebService.PageList.Add(new KillStatsJSON());
            WebService.PageList.Add(new Chat.WordCloudJSON());
            WebService.PageList.Add(new Chat.ClientChatJSON());
            WebService.PageList.Add(new Chat.ChatPage());

            ManagerInstance.GetMessageTokens().Add(new MessageToken("TOTALKILLS", GetTotalKills));
            ManagerInstance.GetMessageTokens().Add(new MessageToken("TOTALPLAYTIME", GetTotalPlaytime));

            ClientStatsSvc = new SharedLibrary.Services.GenericService<Models.EFClientStatistics>();
            ServerSvc = new SharedLibrary.Services.GenericService<Models.EFServer>()

            ChatDB = new ChatDatabase("Database/ChatHistory.rm", ManagerInstance.GetLogger());

            try
            {
                var minimapConfig = MinimapConfig.Read("Config/minimaps.cfg");
            }

            catch (SharedLibrary.Exceptions.SerializeException e)
            {
                MinimapConfig.Write("Config/minimaps.cfg", MinimapConfig.IW4Minimaps());
            }
        }

        public async Task OnUnloadAsync()
        {
            statLists.Clear();
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            try
            {
                if (E.Type == Event.GType.Start)
                {
                    statLists.Add(new StatTracking(S.GetPort()));
                    ServerStats.Add(S.GetPort(), new ServerStatInfo());

                    var config = new ConfigurationManager(S);
                    if (config.GetProperty("EnableTrusted") == null)
                        config.AddProperty(new KeyValuePair<string, object>("EnableTrusted", true));
                }

                if (E.Type == Event.GType.Stop)
                {
                    statLists.RemoveAll(s => s.Port == S.GetPort());
                    ServerStats.Remove(S.GetPort());
                }

                if (E.Type == Event.GType.Connect)
                {
                    ResetCounters(E.Origin.ClientNumber, S.GetPort());

                    var serverStats = statLists.First(s => s.Port == E.Owner.GetPort());
                    var clientStats = await ClientStatsSvc.Get(new int[] { E.Origin.ClientNumber });
                    var server = (await ServerSvc.Find(s => s.Port == E.Owner.GetPort())).First();
                    // create stats if not exist already
                    serverStats.clientStats[E.Origin.ClientNumber] = clientStats ?? await ClientStatsSvc.Create(new Models.EFClientStatistics()
                    {
                        Active = false,
                        Client = E.Target,
                        ClientId = E.Target.ClientId,
                        Deaths = 0,
                        KDR = 0,
                        Kills = 0,
                        Server = (await ServerSvc.Find(s => s.Port == E.Owner.GetPort())).First(),
                        ServerId = server.ServerId,
                        Skill = 0,
                        SPM = 0,
                    });

                    /* var config = new ConfigurationManager(E.Owner);

                 if (!(bool)config.GetProperty("EnableTrusted"))
                       return;

                   PlayerStats checkForTrusted = statLists.Find(x => x.Port == S.GetPort()).playerStats.GetStats(E.Origin);
                   //todo: move this out of here!!
                   if (checkForTrusted.TotalPlayTime >= 4320 && E.Origin.Level < Player.Permission.Trusted && E.Origin.Level != Player.Permission.Flagged)
                   {
                       E.Origin.Level = Player.Permission.Trusted;
                       await E.Owner.Manager.GetDatabase().UpdateClient(E.Origin);
                       await E.Origin.Tell("Congratulations, you are now a ^5trusted ^7player! Type ^5!help ^7to view new commands.");
                       await E.Origin.Tell("You earned this by playing for ^53 ^7full days!");
                   }*/
                }

                if (E.Type == Event.GType.MapEnd || E.Type == Event.GType.Stop)
                {
                    foreach (Player P in S.GetPlayersAsList())
                    {

                        if (P == null)
                            continue;

                        CalculateAndSaveSkill(P, statLists.Find(x => x.Port == S.GetPort()));
                        ResetCounters(P.ClientNumber, S.GetPort());

                        E.Owner.Logger.WriteInfo($"Updated skill for {P}");
                        //E.Owner.Log.Write(String.Format("\r\nJoin: {0}\r\nInactive Minutes: {1}\r\nnewPlayTime: {2}\r\nnewSPM: {3}\r\nkdrWeight: {4}\r\nMultiplier: {5}\r\nscoreWeight: {6}\r\nnewSkillFactor: {7}\r\nprojectedNewSkill: {8}\r\nKills: {9}\r\nDeaths: {10}", connectionTime[P.ClientNumber].ToShortTimeString(), inactiveMinutes[P.ClientNumber], newPlayTime, newSPM, kdrWeight, Multiplier, scoreWeight, newSkillFactor, disconnectStats.Skill, disconnectStats.Kills, disconnectStats.Deaths));
                    }
                }

                if (E.Type == Event.GType.MapChange)
                {
                    ServerStats[S.GetPort()].GetKillQueue().Clear();
                    ServerStats[S.GetPort()].RoundStartTime = DateTime.Now;
                }
            }
    }
    


  