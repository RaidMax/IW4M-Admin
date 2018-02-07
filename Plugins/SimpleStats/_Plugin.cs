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
    public class CViewStats : Command
    {
        public CViewStats() : base("stats", "view your stats", "xlrstats", Player.Permission.User, false, new CommandArgument[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = false
                }
            })
        { }

        public override async Task ExecuteAsync(Event E)
        {

            if (E.Origin.ClientNumber < 0)
            {
                await E.Origin.Tell("You must be ingame to view your stats");
                return;
            }

            String statLine;
            EFClientStatistics pStats;

            if (E.Data.Length > 0 && E.Target == null)
            {
                await E.Origin.Tell("Cannot find the player you specified");
                return;
            }

            if (E.Target != null)
            {
                pStats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).clientStats[E.Origin.ClientNumber];
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            else
            {
                pStats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).clientStats[E.Origin.ClientNumber];
                statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            }

            if (E.Message.IsBroadcastCommand())
            {
                string name = E.Target == null ? E.Origin.Name : E.Target.Name;
                await E.Owner.Broadcast($"Stats for ^5{name}^7");
                await E.Owner.Broadcast(statLine);
            }

            else
            {
                if (E.Target != null)
                    await E.Origin.Tell($"Stats for ^5{E.Target.Name}^7");
                await E.Origin.Tell(statLine);
            }
        }
    }

    public class CViewTopStats : Command
    {
        public CViewTopStats() :
            base("topstats", "view the top 5 players on this server", "ts", Player.Permission.User, false)
        { }

        public override async Task ExecuteAsync(Event E)
        {
            List<KeyValuePair<String, PlayerStats>> pStats = Stats.statLists.Find(x => x.Port == E.Owner.GetPort()).playerStats.GetTopStats();
            StringBuilder msgBlder = new StringBuilder();

            await E.Origin.Tell("^5--Top Players--");
            foreach (KeyValuePair<String, PlayerStats> pStat in pStats)
            {
            /*    Player P = E.Owner.Manager.GetDatabase().GetClient(pStat.Key) as Player;
                if (P == null)
                    continue;
                await E.Origin.Tell(String.Format("^3{0}^7 - ^5{1} ^7KDR | ^5{2} ^7SKILL", P.Name, pStat.Value.KDR, pStat.Value.Skill));*/
            }
        }
    }


    public class CResetStats : Command
    {
        public CResetStats() : base("resetstats", "reset your stats to factory-new", "rs", Player.Permission.User, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            if (E.Origin.ClientNumber >= 0)
            {
                var svc = new SharedLibrary.Services.GenericService<EFClientStatistics>();
                var stats = Stats.statLists[E.Owner.GetPort()].clientStats[E.Origin.ClientNumber];
                await svc.Delete(stats);
                await E.Origin.Tell("Your stats have been reset");
            }

            else
            {
                await E.Origin.Tell("You must be connected to a server to reset your stats");
            }
        }
    }

    public class CPruneAdmins : Command
    {
        public CPruneAdmins() : base("prune", "demote any admins that have not connected recently (defaults to 30 days)", "p", Player.Permission.Owner, false, new CommandArgument[]
        {
            new CommandArgument()
            {
                Name = "inactive days",
                Required = false
            }
        })
        { }

        public override async Task ExecuteAsync(Event E)
        {
            int inactiveDays = 30;

            try
            {
                if (E.Data.Length > 0)
                {
                    inactiveDays = Int32.Parse(E.Data);
                    if (inactiveDays < 1)
                        throw new FormatException();
                }
            }

            catch (FormatException)
            {
                await E.Origin.Tell("Invalid number of inactive days");
                return;
            }

            var inactiveAdmins = await E.Owner.Manager.GetDatabase().PruneInactivePrivilegedClients(inactiveDays);
            await E.Origin.Tell($"Pruned inactive {inactiveAdmins.Count} privileged users");

        }
    }

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

        public class KillInfo
        {
            public IW4Info.HitLocation HitLoc { get; set; }
            public string HitLocString => HitLoc.ToString();
            public IW4Info.MeansOfDeath DeathType { get; set; }
            public string DeathTypeString => DeathType.ToString();
            public int Damage { get; set; }
            public IW4Info.WeaponName Weapon { get; set; }
            public string WeaponString => Weapon.ToString();
            public Vector3 KillOrigin { get; set; }
            public Vector3 DeathOrigin { get; set; }
            // http://wiki.modsrepository.com/index.php?title=Call_of_Duty_5:_Gameplay_standards for conversion to meters
            public double Distance => Vector3.Distance(KillOrigin, DeathOrigin) * 0.0254;
            public string KillerPlayer { get; set; }
            public int KillerPlayerID { get; set; }
            public string VictimPlayer { get; set; }
            public int VictimPlayerID { get; set; }
            public IW4Info.MapName Map { get; set; }
            public int ID => GetHashCode();

            public KillInfo() { }

            public KillInfo(int killer, int victim, string map, string hit, string type, string damage, string weapon, string kOrigin, string dOrigin)
            {
                KillerPlayerID = killer;
                VictimPlayerID = victim;
                Map = ParseEnum<IW4Info.MapName>.Get(map, typeof(IW4Info.MapName));
                HitLoc = ParseEnum<IW4Info.HitLocation>.Get(hit, typeof(IW4Info.HitLocation));
                DeathType = ParseEnum<IW4Info.MeansOfDeath>.Get(type, typeof(IW4Info.MeansOfDeath));
                Damage = Int32.Parse(damage);
                Weapon = ParseEnum<IW4Info.WeaponName>.Get(weapon, typeof(IW4Info.WeaponName));
                KillOrigin = Vector3.Parse(kOrigin);
                DeathOrigin = Vector3.Parse(dOrigin);
            }
        }

        public static List<StatTracking> statLists;

        public class StatTracking
        {
            public DateTime[] lastKill, connectionTime;
            public int[] inactiveMinutes, Kills, deathStreaks, killStreaks;
            public int Port;
            public Models.EFClientStatistics[] clientStats;

            public StatTracking(int port)
            {
                clientStats = new Models.EFClientStatistics[18];
                inactiveMinutes = new int[18];
                Kills = new int[18];
                deathStreaks = new int[18];
                killStreaks = new int[18];
                lastKill = new DateTime[18];
                connectionTime = new DateTime[18];
                Port = port;
            }
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

                if (E.Type == Event.GType.Disconnect)
                {
                    CalculateAndSaveSkill(E.Origin, statLists.Find(x => x.Port == S.GetPort()));
                    ResetCounters(E.Origin.ClientNumber, S.GetPort());
                    E.Owner.Logger.WriteInfo($"Updated skill for disconnecting client {E.Origin}");
                }

                if (E.Type == Event.GType.Kill)
                {
                    if (E.Origin == E.Target || E.Origin == null)
                        return;

                    string[] killInfo = (E.Data != null) ? E.Data.Split(';') : new string[0];

                    if (killInfo.Length >= 9 && killInfo[0].Contains("ScriptKill"))
                    {
                        var killEvent = new KillInfo(E.Origin.ClientNumber, E.Target.ClientNumber, S.CurrentMap.Name, killInfo[7], killInfo[8], killInfo[5], killInfo[6], killInfo[3], killInfo[4])
                        {
                            KillerPlayer = E.Origin.Name,
                            VictimPlayer = E.Target.Name,
                        };

                        if (ServerStats[S.GetPort()].GetKillQueue().Count > MAX_KILLEVENTS - 1)
                            ServerStats[S.GetPort()].GetKillQueue().Dequeue();
                        ServerStats[S.GetPort()].GetKillQueue().Enqueue(killEvent);
                        //S.Logger.WriteInfo($"{E.Origin.Name} killed {E.Target.Name} with a {killEvent.Weapon} from a distance of {Vector3.Distance(killEvent.KillOrigin, killEvent.DeathOrigin)} with {killEvent.Damage} damage, at {killEvent.HitLoc}");
                        var cs = statLists.Find(x => x.Port == S.GetPort());
                        cs.playerStats.AddKill(killEvent);
                    }

                    Player Killer = E.Origin;
                    StatTracking curServer = statLists.Find(x => x.Port == S.GetPort());
                    var killerStats = curServer.clientStats[]

                    if (killerStats == null)
                        killerStats = new PlayerStats(0, 0, 0, 0, 0, 0);

                    curServer.lastKill[E.Origin.ClientNumber] = DateTime.Now;
                    curServer.Kills[E.Origin.ClientNumber]++;

                    if ((DateTime.Now - curServer.lastKill[E.Origin.ClientNumber]).TotalSeconds > 120)
                        curServer.inactiveMinutes[E.Origin.ClientNumber] += 2;

                    killerStats.Kills++;

                    killerStats.KDR = (killerStats.Deaths == 0) ? killerStats.Kills : killerStats.KDR = Math.Round((double)killerStats.Kills / (double)killerStats.Deaths, 2);



                    curServer.playerStats.UpdateStats(Killer, killerStats);

                    curServer.killStreaks[Killer.ClientNumber] += 1;
                    curServer.deathStreaks[Killer.ClientNumber] = 0;

                    await Killer.Tell(MessageOnStreak(curServer.killStreaks[Killer.ClientNumber], curServer.deathStreaks[Killer.ClientNumber]));
                }

                if (E.Type == Event.GType.Death)
                {
                    if (E.Origin == E.Target || E.Origin == null)
                        return;

                    Player Victim = E.Origin;
                    StatTracking curServer = statLists.Find(x => x.Port == S.GetPort());
                    PlayerStats victimStats = curServer.playerStats.GetStats(Victim);

                    if (victimStats == null)
                        victimStats = new PlayerStats(0, 0, 0, 0, 0, 0);

                    victimStats.Deaths++;
                    victimStats.KDR = Math.Round(victimStats.Kills / (double)victimStats.Deaths, 2);

                    curServer.playerStats.UpdateStats(Victim, victimStats);

                    curServer.deathStreaks[Victim.ClientNumber] += 1;
                    curServer.killStreaks[Victim.ClientNumber] = 0;

                    await Victim.Tell(MessageOnStreak(curServer.killStreaks[Victim.ClientNumber], curServer.deathStreaks[Victim.ClientNumber]));
                }

                if (E.Type == Event.GType.Say)
                {
                    ChatDB.AddChatHistory(E.Origin.ClientNumber, E.Owner.GetPort(), E.Data);
                }
            }

            catch (Exception e)
            {
                S.Logger.WriteWarning("StatsPlugin::OnEventAsync failed to complete");
                S.Logger.WriteDebug($"Server:{S}\r\nOrigin:{E.Origin}\r\nTarget:{E.Target}");
                S.Logger.WriteDebug($"Exception: {e.Message}");
            }
        }

        public static string GetTotalKills()
        {
            long Kills = 0;
            foreach (var S in statLists)
                Kills += S.playerStats.GetTotalServerKills();
            return Kills.ToString("#,##0");
        }

        public static string GetTotalPlaytime()
        {
            long Playtime = 0;
            foreach (var S in statLists)
                Playtime += S.playerStats.GetTotalServerPlaytime();
            return Playtime.ToString("#,##0");
        }

        private void CalculateAndSaveSkill(Player P, StatTracking curServer)
        {
            if (P == null)
                return;

            var DisconnectingPlayerStats = curServer.clientStats[P.ClientNumber];

            if (curServer.Kills[P.ClientNumber] == 0)
                return;

            //else if (curServer.lastKill[P.ClientNumber] > curServer.connectionTime[P.ClientNumber])
            // curServer.inactiveMinutes[P.ClientNumber] += (int)(DateTime.Now - curServer.lastKill[P.ClientNumber]).TotalMinutes;

            int newPlayTime = (int)(DateTime.Now - P.LastConnection).TotalMinutes;
            // (int)(DateTime.Now - curServer.connectionTime[P.ClientNumber]).TotalMinutes - curServer.inactiveMinutes[P.ClientNumber];
            // calculate the players Score Per Minute for the current session
            double SessionSPM = curServer.Kills[P.ClientNumber] * 100 / Math.Max(1, newPlayTime);
            // calculate how much the KDR should way
            // 1.637 is a Eddie-Generated number that weights the KDR nicely
            double KDRWeight = Math.Round(Math.Pow(DisconnectingPlayerStats.KDR, 1.637 / Math.E), 3);
            double SPMWeightAgainstAverage;

            // if no SPM, weight is 1 else the weight is the current sessions spm / lifetime average score per minute
            SPMWeightAgainstAverage = (DisconnectingPlayerStats.SPM == 1) ? 1 : SessionSPM / DisconnectingPlayerStats.SPM;

            // calculate the weight of the new play time againmst lifetime playtime
            double SPMAgainstPlayWeight = newPlayTime / Math.Min(600, P.TotalConnectionTime + newPlayTime);
            // calculate the new weight against average times the weight against play time
            double newSkillFactor = SPMWeightAgainstAverage * SPMAgainstPlayWeight * SessionSPM;

            // if the weight is greater than 1, add, else subtract
            DisconnectingPlayerStats.SPM += (SPMWeightAgainstAverage >= 1) ? newSkillFactor : -newSkillFactor;

            DisconnectingPlayerStats.Skill = DisconnectingPlayerStats.SPM * KDRWeight * 10;

            ClientStatsSvc.Update(DisconnectingPlayerStats);
        }

        private void ResetCounters(int cID, int serverPort)
        {
            StatTracking selectedPlayers = statLists.Find(x => x.Port == serverPort);

            if (selectedPlayers == null)
                return;

            selectedPlayers.Kills[cID] = 0;
            selectedPlayers.connectionTime[cID] = DateTime.Now;
            selectedPlayers.inactiveMinutes[cID] = 0;
            selectedPlayers.deathStreaks[cID] = 0;
            selectedPlayers.killStreaks[cID] = 0;
        }

        private String MessageOnStreak(int killStreak, int deathStreak)
        {
            String Message = "";
            switch (killStreak)
            {
                case 5:
                    Message = "Great job! You're on a ^55 killstreak!";
                    break;
                case 10:
                    Message = "Amazing! ^510 kills ^7without dying!";
                    break;
            }

            switch (deathStreak)
            {
                case 5:
                    Message = "Pick it up soldier, you've died ^55 times ^7in a row...";
                    break;
                case 10:
                    Message = "Seriously? ^510 deaths ^7without getting a kill?";
                    break;
            }

            return Message;
        }
    }


    public class PlayerStats
    {
        public PlayerStats(int K, int D, double DR, double S, double sc, int P)
        {
            Kills = K;
            Deaths = D;
            KDR = DR;
            Skill = S;
            scorePerMinute = sc;
            TotalPlayTime = P;
        }

        public int Kills;
        public int Deaths;
        public double KDR;
        public double Skill;
        public double scorePerMinute;
        public int TotalPlayTime;
    }
}