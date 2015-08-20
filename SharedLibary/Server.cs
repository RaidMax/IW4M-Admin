using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public abstract class Server
    {
        public Server(string address, int port, string password, int H, int PID)
        {
            this.PID = PID;
            Handle = H;
            IP = address;
            Port = port;
            clientnum = 0;
            logFile = new IFile("admin_" + port + ".log", true);
#if DEBUG
            Log = new Log(logFile, Log.Level.Debug, port);
#else
            Log = new Log(logFile, Log.Level.Production, port);
#endif
            clientDB = new ClientsDB("clients.rm");
            statDB = new StatsDB("stats_" + Port + ".rm");
            aliasDB = new AliasesDB("aliases.rm");

            players = new List<Player>(new Player[18]);
            events = new Queue<Event>();
            Macros = new Dictionary<String, Object>();
            Reports = new List<Report>();
            statusPlayers = new Dictionary<string, Player>();
            chatHistory = new List<Chat>();
            lastWebChat = DateTime.Now;
            nextMessage = 0;
            initCommands();
            initMacros();
            initMessages();
            initMaps();
            initRules();
        }

        //Returns the current server name -- *STRING*
        public String getName()
        {
            return hostname;
        }

        public String getMap()
        {
            return mapname;
        }

        public String getGametype()
        {
            return Gametype;
        }

        //Returns current server IP set by `net_ip` -- *STRING*
        public String getIP()
        {
            return IP;
        }

        //Returns current server port set by `net_port` -- *INT*
        public int getPort()
        {
            return Port;
        }

        //Returns number of active clients on server -- *INT*
        public int getNumPlayers()
        {
            return clientnum;
        }

        //Returns the list of commands
        public List<Command> getCommands()
        {
            return commands;
        }

        //Returns list of all current players
        public List<Player> getPlayers()
        {
            return players;
        }

        public int getClientNum()
        {
            return clientnum;
        }

        public int getMaxClients()
        {
            return maxClients;
        }

        //Returns list of all active bans (loaded at runtime)
        public List<Ban> getBans()
        {
            return Bans;
        }

        public int pID()
        {
            return this.PID;
        }

        abstract public void getAliases(List<Player> returnPlayers, Player Origin);
 
        //Add player object p to `players` list
        abstract public bool addPlayer(Player P);

        //Remove player by CLIENT NUMBER
        abstract public bool removePlayer(int cNum);

        abstract public Player clientFromEventLine(String[] L, int cIDPos);

        public Player clientFromName(String pName)
        {
            lock (players)
            {
                foreach (var P in players)
                {
                    if (P != null && P.Name.ToLower().Contains(pName.ToLower()))
                        return P;
                }
            }

            return null;
        }

        //Check ban list for every banned player and return ban if match is found 
        abstract public Ban isBanned(Player C);

        //Procses requested command correlating to an event
        abstract public Command processCommand(Event E, Command C);

        //push a new event into the queue
        private void addEvent(Event E)
        {
            events.Enqueue(E);
        }

        abstract public void executeCommand(String CMD);

        abstract public dvar getDvar(String DvarName);

        abstract public void setDvar(String Dvar, String Value);

        //Starts the monitoring process
        abstract public void Monitor();

        abstract public bool intializeBasics();

        //Process any server event
        abstract public bool processEvent(Event E);

        public bool Reload()
        {
            try
            {
                messages = null;
                maps = null;
                rules = null;
                initMaps();
                initMessages();
                initRules();
                return true;
            }
            catch (Exception E)
            {
                Log.Write("Unable to reload configs! - " + E.Message, Log.Level.Debug);
                messages = new List<String>();
                maps = new List<Map>();
                rules = new List<String>();
                return false;
            }
        }

        //THESE MAY NEED TO BE MOVED
        public void Broadcast(String Message)
        {
            executeCommand("sayraw " + Message);
        }

        public void Tell(String Message, Player Target)
        {
            if (Target.clientID > -1)
                executeCommand("tellraw " + Target.clientID + " " + Message + "^7");
        }

        public void Kick(String Message, Player Target)
        {
            if (Target.clientID > -1)
                executeCommand("clientkick " + Target.clientID + " \"" + Message + "^7\"");
        }

        abstract public void Ban(String Message, Player Target, Player Origin);

        abstract public bool Unban(String GUID, Player Target);


        public void fastRestart(int delay)
        {
            Utilities.Wait(delay);
            executeCommand("fast_restart");
        }

        public void mapRotate(int delay)
        {
            Utilities.Wait(delay);
            executeCommand("map_rotate");
        }

        public void tempBan(String Message, Player Target)
        {
            executeCommand("tempbanclient " + Target.clientID + " \"" + Message + "\"");
        }
        
        public void mapRotate()
        {
            mapRotate(0);
        }

        public void Map(String mapName)
        {
            executeCommand("map " + mapName);
        }

        public void ToAdmins(String message)
        {
            lock (players) // threading can modify list while we do this
            {
                foreach (Player P in players)
                {
                    if (P == null)
                        continue;

                    if (P.Level > Player.Permission.Flagged)
                    {
                        P.Alert();
                        P.Tell(message);
                    }
                }
            }
        }

        public void Alert(Player P)
        {
            executeCommand("admin_lastevent alert;" + P.npID + ";0;mp_killstreak_nuclearstrike");
        }

        public void webChat(Player P, String Message)
        {
            DateTime requestTime = DateTime.Now;

            if ((requestTime - lastWebChat).TotalSeconds > 1)
            {
                Broadcast("^1[WEBCHAT] ^5" + P.Name + "^7 - " + Message);
                while (chatHistory.Count > Math.Ceiling((double)clientnum / 2))
                    chatHistory.RemoveAt(0);

                if (Message.Length > 50)
                    Message = Message.Substring(0, 50) + "...";

                chatHistory.Add(new Chat(P, Utilities.stripColors(Message), DateTime.Now));
                lastWebChat = DateTime.Now;
            }
        }

        abstract public void initMacros();

        private void initMaps()
        {
            maps = new List<Map>();

            IFile mapfile = new IFile("config\\maps.cfg");
            String[] _maps = mapfile.readAll();
            mapfile.Close();
            if (_maps.Length > 2) // readAll returns minimum one empty string
            {
                foreach (String m in _maps)
                {
                    String[] m2 = m.Split(':');
                    if (m2.Length > 1)
                    {
                        Map map = new Map(m2[0].Trim(), m2[1].Trim());
                        maps.Add(map);
                    }
                }
            }     
            else
                Log.Write("Maps configuration appears to be empty - skipping...", Log.Level.All);
        }

        private void initMessages()
        {
            messages = new List<String>();

            IFile messageCFG = new IFile("config\\messages.cfg");
            String[] lines = messageCFG.readAll();
            messageCFG.Close();

            if (lines.Length < 2) //readAll returns minimum one empty string
            {
                Log.Write("Messages configuration appears empty - skipping...", Log.Level.All);
                return;
            }

            int mTime = -1;
            int.TryParse(lines[0], out mTime);

            if (messageTime == -1)
                messageTime = 60;
            else
                messageTime = mTime;
            
            foreach (String l in lines)
            {
                if (lines[0] != l && l.Length > 1)
                    messages.Add(l);
            }

            messageCFG.Close();

            //if (Program.Version != Program.latestVersion && Program.latestVersion != 0)
              // messages.Add("^5IW4M Admin ^7is outdated. Please ^5update ^7to version " + Program.latestVersion);
        }

        private void initRules()
        {
            rules = new List<String>();

            IFile ruleFile = new IFile("config\\rules.cfg");
            String[] _rules = ruleFile.readAll();
            ruleFile.Close();
            if (_rules.Length > 2) // readAll returns minimum one empty string
            {
                foreach (String r in _rules)
                {
                    if (r.Length > 1)
                        rules.Add(r);
                }
            }
            else
                Log.Write("Rules configuration appears empty - skipping...", Log.Level.All);

            ruleFile.Close();
        }

        abstract public void initCommands();
        abstract public void initAbstractObj();

        //Objects
        public Log Log;
        public List<Ban> Bans;
        public Player owner;
        public List<Map> maps;
        public List<String> rules;
        public Queue<Event> events;
        public String Website;
        public String Gametype;
        public int totalKills = 0;
        public List<Report> Reports;
        public List<Chat> chatHistory;

        //Info
        protected String IP;
        protected int Port;
        protected String hostname;
        protected String mapname;
        protected int clientnum;
        protected List<Player> players;
        protected List<Command> commands;
        protected List<String> messages;
        protected int messageTime;
        protected TimeSpan lastMessage;
        protected DateTime lastPoll;
        protected int nextMessage;
        protected String IW_Ver;
        protected int maxClients;
        protected Dictionary<String, Object> Macros;
        protected DateTime lastWebChat;
        protected int Handle;
        protected int PID;
        protected IFile logFile;

        // Will probably move this later
        public Dictionary<String, Player> statusPlayers;
        public bool isRunning;

        // Log stuff
        protected String Basepath;
        protected String Mod;
        protected String logPath;

        // Databases
        public ClientsDB clientDB;
        public AliasesDB aliasDB;
        public StatsDB statDB;
    }
}
