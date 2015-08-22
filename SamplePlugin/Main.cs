using System;
using SharedLibrary;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Data;

namespace SamplePlugin
{
#if SAMPLE_CODE
    public class SampleCommand : Command
    {
        public SampleCommand() : base("testplugin", "sample plugin command. syntax !testplugin", "tp", Player.Permission.User, 0, false) { }

        public override void Execute(Event E)
        {
            Player clientWhoSent = E.Origin;
            Server originatingServer = E.Owner;

            String[] messageToClient = { 
                                           String.Format("The command {0} was requested by ^3{1}", Name, clientWhoSent.Name), 
                                           String.Format("The command was request on server ^1{0}", originatingServer.getName()) 
                                       };

            foreach (String Line in messageToClient)
                clientWhoSent.Tell(Line);
        }
    }

    public class AnotherSampleCommand : Command
    {
        public AnotherSampleCommand() : base("scream", "broadcast your message. syntax !scream <message>", "s", Player.Permission.Moderator, 1, false) { }

        public override void Execute(Event E)
        {
            Server originatingServer = E.Owner;
            String Message = E.Data;
            String Sender = E.Origin.Name;

            for (int i = 0; i < 10; i++)
                originatingServer.Broadcast(String.Format("^7{0}: ^{1}{2}^7", Sender, i, Message));

            originatingServer.Log.Write("This line is coming from the plugin " + this.Name, Log.Level.Production);
        }
    }

    public class SampleEvent : EventNotify
    {
        public override void onLoad()
        {
            Console.WriteLine("The sample event plugin was loaded!");
        }
        public override void onEvent(Event E)
        {
            E.Owner.Broadcast("An event occured of type: ^1" + E.Type);

            if (E.Data != null)
                E.Origin.Tell(E.Data);
        }
    }
    
    public class InvalidCommandExample
    {
        private void doNotDoThis() { }
    }
#endif

    public class StatCommand : Command
    {
        public StatCommand() : base("stats", "view your stats. syntax !stats", "xlrstats", Player.Permission.User, 0, false) { }

        public override void Execute(Event E)
        {
            PlayerStats pStats = Stats.playerStats.getStats(E.Origin);
            String statLine = String.Format("^5{0} ^7KILLS | ^5{1} ^7DEATHS | ^5{2} ^7KDR | ^5{3} ^7SKILL", pStats.Kills, pStats.Deaths, pStats.KDR, pStats.Skill);
            E.Origin.Tell(statLine);
        }
    }

    public class Stats : Plugin
    {
        public static StatsDB playerStats { get; private set; }

        public override void onEvent(Event E)
        {
            if (E.Type == Event.GType.Kill)
            {
                Player Killer = E.Origin;
                PlayerStats killerStats = playerStats.getStats(Killer);

                if (Killer != E.Target)
                {
                    killerStats.Kills++;

                    if (killerStats.Deaths == 0)
                        killerStats.KDR = killerStats.Kills;
                    else
                        killerStats.KDR = killerStats.Kills / killerStats.Deaths;

                    playerStats.updateStats(Killer, killerStats);
                }
            }

            if (E.Type == Event.GType.Death)
            {
                Player Victim = E.Origin;
                PlayerStats victimStats = playerStats.getStats(Victim);

                victimStats.Deaths++;
                victimStats.KDR = victimStats.Kills / victimStats.Deaths;

                playerStats.updateStats(Victim, victimStats);
            }
        }

        public override void onLoad()
        {
            playerStats = new StatsDB("stats.rm");
        }

        public override void onUnload()
        {

        }

        public override string Name
        {
            get { return "Basic Stats"; }
        }

        public override float Version
        {
            get { return 0.1f; }
        }
    }

    public class StatsDB : Database
    {
        public StatsDB(String FN) : base(FN) { }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [STATS] ( [npID] TEXT, [KILLS] INTEGER DEFAULT 0, [DEATHS] INTEGER DEFAULT 0, [KDR] REAL DEFAULT 0, [SKILL] REAL DEFAULT 0, [MEAN] REAL DEFAULT 0, [DEV] REAL DEFAULT 0 );";
                ExecuteNonQuery(Create);
            }
        }

        public void addPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>();

            newPlayer.Add("npID", P.npID);
            newPlayer.Add("KILLS", 0);
            newPlayer.Add("DEATHS", 0);
            newPlayer.Add("KDR", 0);
            newPlayer.Add("SKILL", 0); 

            Insert("STATS", newPlayer);
        }

        public PlayerStats getStats(Player P)
        {
            String Query = String.Format("SELECT * FROM STATS WHERE npID = '{0}'", P.npID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                return new PlayerStats(
                                        Convert.ToInt32(ResponseRow["KILLS"]),
                                        Convert.ToInt32(ResponseRow["DEATHS"]),
                                        Convert.ToDouble(ResponseRow["KDR"]),
                                        Convert.ToDouble(ResponseRow["SKILL"])
                                      );
            }

            else
            {
                addPlayer(P);
                return getStats(P);
            }
        }

        public void updateStats(Player P, PlayerStats S)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>();

            updatedPlayer.Add("KILLS", S.Kills);
            updatedPlayer.Add("DEATHS", S.Deaths);
            updatedPlayer.Add("KDR", Math.Round(S.KDR, 2));
            updatedPlayer.Add("SKILL", S.Skill);

            Update("STATS", updatedPlayer, String.Format("npID = '{0}'", P.npID));
        }
    }

    public struct PlayerStats
    {
        public PlayerStats(int K, int D, double DR, double S)
        {
            Kills = K;
            Deaths = D;
            KDR = DR;
            Skill = S;
        }

        public int Kills;
        public int Deaths;
        public double KDR;
        public double Skill;
    }
}