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
            aliasDB = new AliasesDB("aliases.rm");

            Bans = new List<Penalty>();
            players = new List<Player>(new Player[18]);
            events = new Queue<Event>();
            Macros = new Dictionary<String, Object>();
            Reports = new List<Report>();
            statusPlayers = new Dictionary<string, Player>();
            playerHistory = new Queue<PlayerHistory>();
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
        public List<Penalty> getBans()
        {
            return Bans;
        }

        public int pID()
        {
            return this.PID;
        }

        /// <summary>
        /// Get any know aliases ( name or ip based ) from the database
        /// </summary>
        /// <param name="Origin">Player to scan for aliases</param>
        abstract public List<Aliases> getAliases(Player Origin);

        public List<Player> getPlayerAliases(Player Origin)
        {
            List<int> databaseIDs = new List<int>();

            foreach (Aliases A in getAliases(Origin))
                databaseIDs.Add(A.Number);

            return clientDB.getPlayers(databaseIDs);
        }
 
        /// <summary>
        /// Add a player to the server's player list
        /// </summary>
        /// <param name="P">Player pulled from memory reading</param>
        /// <returns>True if player added sucessfully, false otherwise</returns>
        abstract public bool addPlayer(Player P);

        /// <summary>
        /// Remove player by client number
        /// </summary>
        /// <param name="cNum">Client ID of player to be removed</param>
        /// <returns>true if removal succeded, false otherwise</returns>
        abstract public bool removePlayer(int cNum);

        /// <summary>
        /// Get the player from the server's list by line from game long
        /// </summary>
        /// <param name="L">Game log line containing event</param>
        /// <param name="cIDPos">Position in the line where the cliet ID is written</param>
        /// <returns>Matching player if found</returns>
        abstract public Player clientFromEventLine(String[] L, int cIDPos);

        /// <summary>
        /// Get a player by name
        /// </summary>
        /// <param name="pName">Player name to search for</param>
        /// <returns>Matching player if found</returns>
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

        /// <summary>
        /// Check ban list for every banned player and return ban if match is found 
        /// </summary>
        /// <param name="C">Player to check if banned</param>
        /// <returns>Matching ban if found</returns>
        abstract public Penalty isBanned(Player C);

        /// <summary>
        /// Process requested command correlating to an event
        /// </summary>
        /// <param name="E">Event parameter</param>
        /// <param name="C">Command requested from the event</param>
        /// <returns></returns>
        abstract public Command processCommand(Event E, Command C);

        /// <summary>
        /// Execute a command on the server
        /// </summary>
        /// <param name="CMD">Command to execute</param>
        abstract public void executeCommand(String CMD);

        /// <summary>
        /// Retrieve a Dvar from the server
        /// </summary>
        /// <param name="DvarName">Name of Dvar to retrieve</param>
        /// <returns>Dvar if found</returns>
        abstract public dvar getDvar(String DvarName);

        /// <summary>
        /// Set a Dvar on the server
        /// </summary>
        /// <param name="Dvar">Name of the</param>
        /// <param name="Value"></param>
        abstract public void setDvar(String Dvar, String Value);

        /// <summary>
        /// Main loop for the monitoring processes of the server ( handles events and connects/disconnects )
        /// </summary>
        abstract public void Monitor();

        /// <summary>
        /// Set up the basic variables ( base path / hostname / etc ) that allow the monitor thread to work
        /// </summary>
        /// <returns>True if no issues initializing, false otherwise</returns>
        abstract public bool intializeBasics();

        /// <summary>
        /// Process any server event
        /// </summary>
        /// <param name="E">Event</param>
        /// <returns>True on sucess</returns>
        abstract public bool processEvent(Event E);

        /// <summary>
        /// Reloads all the server configurations
        /// </summary>
        /// <returns>True on sucess</returns>
        abstract public bool Reload();

        /// <summary>
        /// Send a message to all players
        /// </summary>
        /// <param name="Message">Message to be sent to all players</param>
        public void Broadcast(String Message)
        {
            executeCommand("sayraw " + Message);
        }
        
        /// <summary>
        /// Send a message to a particular players
        /// </summary>
        /// <param name="Message">Message to send</param>
        /// <param name="Target">Player to send message to</param>
        public void Tell(String Message, Player Target)
        {
            if (Target.clientID > -1 && Message.Length > 0)
                executeCommand("tellraw " + Target.clientID + " " + Message + "^7");

            if (Target.Level == Player.Permission.Console)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(Utilities.stripColors(Message));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        /// <summary>
        /// Send a message to all admins on the server
        /// </summary>
        /// <param name="message">Message to send out</param>
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

        /// <summary>
        /// Alert a player via gsc implementation
        /// </summary>
        /// <param name="P"></param>
        public void Alert(Player P)
        {
            executeCommand("admin_lastevent alert;" + P.npID + ";0;mp_killstreak_nuclearstrike");
        }

        /// <summary>
        /// Kick a player from the server
        /// </summary>
        /// <param name="Reason">Reason for kicking</param>
        /// <param name="Target">Player to kick</param>
        abstract public void Kick(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Temporarily ban a player ( default 1 hour ) from the server
        /// </summary>
        /// <param name="Reason">Reason for banning the player</param>
        /// <param name="Target">The player to ban</param>
        abstract public void tempBan(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Perm ban a player from the server
        /// </summary>
        /// <param name="Reason">The reason for the ban</param>
        /// <param name="Target">The person to ban</param>
        /// <param name="Origin">The person who banned the target</param>
        abstract public void Ban(String Reason, Player Target, Player Origin);

        abstract public void Warn(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Unban a player by npID / GUID
        /// </summary>
        /// <param name="npID">npID of the player</param>
        /// <param name="Target">I don't remember what this is for</param>
        /// <returns></returns>
        abstract public bool Unban(String npID, Player Target);

        /// <summary>
        /// Fast restart the server with a specified delay
        /// </summary>
        /// <param name="delay"></param>
        public void fastRestart(int delay)
        {
            Utilities.Wait(delay);
            executeCommand("fast_restart");
        }

        /// <summary>
        /// Rotate the server to the next map with specified delay
        /// </summary>
        /// <param name="delay"></param>
        public void mapRotate(int delay)
        {
            Utilities.Wait(delay);
            executeCommand("map_rotate");
        }
        
        /// <summary>
        /// Map rotate without delay
        /// </summary>
        public void mapRotate()
        {
            mapRotate(0);
        }

        /// <summary>
        /// Change the current searver map
        /// </summary>
        /// <param name="mapName">Non-localized map name</param>
        public void Map(String mapName)
        {
            executeCommand("map " + mapName);
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

        /// <summary>
        /// Initalize the macro variables
        /// </summary>
        abstract public void initMacros();

        /// <summary>
        /// Read the map configuration
        /// </summary>
        protected void initMaps()
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

        /// <summary>
        /// Initialize the messages to be broadcasted
        /// </summary>
        protected void initMessages()
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

        /// <summary>
        /// Initialize the rules configuration
        /// </summary>
        protected void initRules()
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

        /// <summary>
        /// Load up the built in commands
        /// </summary>
        abstract public void initCommands();

        //Objects
        public Log Log { get; private set; }
        public List<Penalty> Bans;
        public Player owner;
        public List<Map> maps;
        public List<String> rules;
        public Queue<Event> events;
        public String Website;
        public String Gametype;
        public int totalKills = 0;
        public List<Report> Reports;
        public List<Chat> chatHistory;
        public Queue<PlayerHistory> playerHistory { get; private set; }

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
    }
}
