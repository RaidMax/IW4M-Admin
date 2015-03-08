
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading; //SLEEP
using System.IO;


namespace IW4MAdmin
{
    class Server
    {
        const int FLOOD_TIMEOUT = 300;

        public Server(string address, int port, string password)
        {
            IP = address;
            Port = port;
            rcon_pass = password;
            clientnum = 0;
            RCON = new RCON(this);
            logFile = new file("admin_" + address + "_" + port + ".log", true);
            Log = new Log(logFile, Log.Level.Production);
            players = new List<Player>(new Player[18]);
            DB = new Database(port + ".db");
            Bans = DB.getBans();
            owner = DB.getOwner();
            maps = new List<Map>();
            rules = new List<String>();
            messages = new List<String>();
            events = new Queue<Event>();
            nextMessage = 0;
            initCommands();
            initMessages();
            initMaps();
            initRules();
        }

        //Returns the current server name -- *STRING*
        public String getName()
        {
            return hostname;
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

        public List<Player> getPlayers()
        {
            return players;
        }

        public List<Ban> getBans()
        {
            return Bans;
        }

        //Performs update on server statistics read from log.
        public void Update()
        {
        }
            
        //Add player object p to `players` list
        public bool addPlayer(Player P)
        {
            try
            {
                if (DB.getPlayer(P.getID(), P.getClientNum()) == null)
                    DB.addPlayer(P);
                else
                {
                    //messy way to prevent loss of last event
                    Player A;
                    A = DB.getPlayer(P.getID(), P.getClientNum());
                    A.lastEvent = P.lastEvent;
                    P = A;
                }

                players[P.getClientNum()] = null;
                players[P.getClientNum()] = P;

                clientnum++;

                if (P.getLevel() == Player.Permission.Banned)
                {
                    Log.Write("Banned client " + P.getName() + " trying to connect...", Log.Level.Production);
                    String Message = "^1Player Kicked: ^7Previously Banned for ^5" + isBanned(P).getReason();
                    P.Kick(Message);
                }

                else
                    Log.Write("Client " + P.getName() + " connecting...", Log.Level.Production);

                return true;
            }
            catch (Exception E)
            {
                Log.Write("Unable to add player - " + E.Message, Log.Level.Debug);
                return false;
            }
        }

        //Remove player by CLIENT NUMBER
        public bool removePlayer(int cNum)
        {
            Log.Write("Client at " + cNum + " disconnecting...", Log.Level.Production);
            players[cNum] = null;
            clientnum--;
            return true;
        }

         public Player clientFromLine(String[] line, int name_pos, bool create)
         {
            string Name = line[name_pos].ToString().Trim();
            if (create)
            {
                Player C = new Player(Name, line[1].ToString(), Convert.ToInt16(line[2]), 0);
                return C;
            }

            else
            {
                foreach (Player P in players)
                {
                    if (P == null)
                        continue; 

                    if (line[1].Trim() == P.getID())
                        return P;
                }

                Log.Write("Could not find player but player is in server. Lets try to manually add (looks like you didn't start me on an empty server)", Log.Level.All);
                addPlayer(new Player(Name, line[1].ToString(), Convert.ToInt16(line[2]), 0));
                return players[Convert.ToInt16(line[2])];
            }
        }

         public Player clientFromLine(String Name)
         {
             foreach (Player P in players)
             {
                 if (P == null)
                     continue;
                 if (P.getName().ToLower().Contains(Name.ToLower()))
                     return P;
             }

             return null;
         }

         public Ban isBanned(Player C)
         {
             if (C.getLevel() == Player.Permission.Banned)
             {
                 foreach (Ban B in Bans)
                 {
                     if (B.getID() == C.getID())
                         return B;
                 }
             }

            return null;
         }

        public Command processCommand(Event E, Command C)
        {
            E.Data = Utilities.removeWords(E.Data, 1);
            String[] Args = E.Data.Trim().Split(' ');
   
            if (Args.Length < (C.getNumArgs()))
            {
                E.Origin.Tell("Not enough arguments supplied!");
                return null;
            }

            if(E.Origin.getLevel() < C.getNeededPerm())
            {
                E.Origin.Tell("You do not have access to that command!");
                return null;
            }

            if (C.needsTarget())
            {
                int cNum = -1;
                int.TryParse(Args[0], out cNum);

                if (Args[0][0] == '@')
                {
                    int dbID = -1;
                    int.TryParse(Args[0].Substring(1, Args[0].Length-1), out dbID);
                    Player found = E.Owner.DB.findPlayers(dbID);
                    if (found != null)
                        E.Target = found;
                }

                else if(Args[0].Length < 3 && cNum > -1 && cNum < 18)
                {
                    if (players[cNum] != null)
                        E.Target = players[cNum];
                }

                else
                    E.Target = clientFromLine(Args[0]);

                if (E.Target == null)
                {
                    E.Origin.Tell("Unable to find specified player.");
                    return null;
                }
            }
            return C;
        }

        private void addEvent(Event E)
        {
            events.Enqueue(E);
        }

        private void manageEventQueue()
        {
            while (true)
            {
                if (events.Count > 0)
                {
                    processEvent(events.Peek());
                    events.Dequeue();
                }
                Utilities.Wait(0.1);
            }
        }

        //Starts the monitoring process
        public void Monitor()
        {
            if (!intializeBasics())
            {
                Log.Write("Shutting due to uncorrectable errors..." + logPath, Log.Level.Debug);
                Utilities.Wait(10);
                Environment.Exit(-1);
            }

            //Handles new rcon requests in a fashionable manner
            Thread RCONQueue = new Thread(new ThreadStart(RCON.ManageRCONQueue));
            RCONQueue.Start();

            //Handles new events in a fashionable manner
            Thread eventQueue = new Thread(new ThreadStart(manageEventQueue));
            eventQueue.Start();

            long l_size = -1;
            String[] lines = new String[8];
            String[] oldLines = new String[8];
            DateTime start = DateTime.Now;

            Utilities.Wait(1);
            Broadcast("IW4M Admin is now ^2ONLINE");

            while (errors <=5)
            {
                try
                {
                    lastMessage = DateTime.Now - start;
                    if(lastMessage.TotalSeconds > messageTime && messages.Count > 0)
                    {
                        Broadcast(messages[nextMessage]);
                        if (nextMessage == (messages.Count - 1))
                            nextMessage = 0;
                        else
                            nextMessage++;
                        start = DateTime.Now;
                    }

                    if (l_size != logFile.getSize())
                    {
                        lines = logFile.Tail(8);
                        if (lines != oldLines)
                        {
                            l_size = logFile.getSize();
                            int end;
                            if (lines.Length == oldLines.Length)
                                end = lines.Length - 1;
                            else
                                end = Math.Abs((lines.Length - oldLines.Length)) - 1;

                            for (int count = 0; count < lines.Length; count++)
                            {
                                if (lines.Length < 1 && oldLines.Length < 1)
                                    continue;

                                if (lines[count] == oldLines[oldLines.Length - 1])
                                    continue;

                                if (lines[count].Length < 10) //Not a needed line 
                                    continue;

                                else
                                {
                                    string[] game_event = lines[count].Split(';');
                                    Event event_ = Event.requestEvent(game_event, this);
                                    if (event_ != null)
                                    {
                                        if (event_.Origin == null)
                                            event_.Origin = new Player("WORLD", "-1", -1, 0);

                                        event_.Origin.lastEvent = event_;
                                        event_.Origin.lastEvent.Owner = this;

                                        addEvent(event_);
                                    }
                                }

                            }
                        }
                    }
                    oldLines = lines;
                    l_size = logFile.getSize();
                    Thread.Sleep(1);
                }
                catch (Exception E)
                {
                    Log.Write("Something unexpected occured. Hopefully we can ignore it - " + E.Message + " @"  + Utilities.GetLineNumber(E), Log.Level.All);
                    errors++;
                    continue;
                }

            }

            RCONQueue.Abort();
            eventQueue.Abort();

        }

        private bool intializeBasics()
        {
            try
            {
                //GET fs_basepath
                String[] p = RCON.responseSendRCON("fs_basepath");

                if (p == null)
                {
                    Log.Write("Could not obtain basepath!", Log.Level.All);
                    return false;
                }

                p = p[1].Split('"');
                Basepath = p[3].Substring(0, p[3].Length - 2).Trim();
                p = null;

                Thread.Sleep(FLOOD_TIMEOUT);
                //END

                //get fs_game
                p = RCON.responseSendRCON("fs_game");

                if (p == null)
                {
                    Log.Write("Could not obtain mod path!", Log.Level.All);
                    return false;
                }

                p = p[1].Split('"');
                Mod = p[3].Substring(0, p[3].Length - 2).Trim().Replace('/', '\\');
                p = null;

                Thread.Sleep(FLOOD_TIMEOUT);
                //END

                //get g_log
                p = RCON.responseSendRCON("g_log");

                if (p == null)
                {
                    Log.Write("Could not obtain log path!", Log.Level.All);
                    return false;
                }

                if (p.Length < 4)
                {
                    Thread.Sleep(FLOOD_TIMEOUT);
                    Log.Write("Server does not appear to have map loaded. Please map_rotate", Log.Level.All);
                    return false;
                }

                p = p[1].Split('"');
                string log = p[3].Substring(0, p[3].Length - 2).Trim();
                p = null;

                Thread.Sleep(FLOOD_TIMEOUT);
                //END

                //get g_logsync
                p = RCON.responseSendRCON("g_logsync");

                if (p == null)
                {
                    Log.Write("Could not obtain log sync status!", Log.Level.All);
                    return false;
                }


                p = p[1].Split('"');
                int logsync = Convert.ToInt32(p[3].Substring(0, p[3].Length - 2).Trim());
                p = null;

                Thread.Sleep(FLOOD_TIMEOUT);
                if (logsync != 1)
                    RCON.sendRCON("g_logsync 1");

                Thread.Sleep(FLOOD_TIMEOUT);
                //END

                //get iw4m_onelog
                p = RCON.responseSendRCON("iw4m_onelog");

                if (p[0] == String.Empty || p[1].Length < 15)
                {
                    Log.Write("Could not obtain iw4m_onelog value!", Log.Level.All);
                    return false;
                }

                p = p[1].Split('"');
                string onelog = p[3].Substring(0, p[3].Length - 2).Trim();
                p = null;
                //END

                Thread.Sleep(FLOOD_TIMEOUT);

                //get sv_hostname
                p = RCON.responseSendRCON("sv_hostname");

                if (p == null)
                {
                    Log.Write("Could not obtain server name!", Log.Level.All);
                    return false;
                }

                p = p[1].Split('"');
                hostname = p[3].Substring(0, p[3].Length - 2).Trim();
                p = null;
                //END

                if (Mod == String.Empty || onelog == "1")
                    logPath = Basepath + '\\' + "m2demo" + '\\' + log;
                else
                    logPath = Basepath + '\\' + Mod + '\\' + log;

                if (!File.Exists(logPath))
                {
                    Log.Write("Gamelog does not exist!", Log.Level.All);
                    return false;
                }

                logFile = new file(logPath);
                Log.Write("Log file is " + logPath, Log.Level.Debug);

                return true;
            }
            catch (Exception E)
            {
                Log.Write("Error during initialization - " + E.Message, Log.Level.All);
                return false;
            }
        }

        //Process any server event
        public bool processEvent(Event E)
        {
            if (E.Type == Event.GType.Connect)
            {
                this.addPlayer(E.Origin);
                return true;
            }

            if (E.Type == Event.GType.Disconnect)
            {
                if (getNumPlayers() > 0)
                    removePlayer(E.Origin.getClientNum());
                return true;
            }

            if (E.Type == Event.GType.Say)
            {
                Log.Write("Message from " + E.Origin.getName() + ": " + E.Data, Log.Level.Debug);

                if (E.Data.Substring(0, 1) != "!")
                    return true;

                Command C = E.isValidCMD(commands);
                if (C != null)
                {
                    C = processCommand(E, C);
                    if (C != null)
                    {
                        C.Execute(E);
                        return true;
                    }
                    else
                    {
                        Log.Write("Error processing command by " + E.Origin.getName(), Log.Level.Debug);
                        return true;
                    }
                }

                else
                    E.Origin.Tell("You entered an invalid command!");

                return true;
            }

            if (E.Type == Event.GType.MapChange)
            {
                Log.Write("Map change detected..", Log.Level.Production);
                return true;
                //TODO here
            }

            if (E.Type == Event.GType.MapEnd)
            {
                Log.Write("Game ending...", Log.Level.Production);
                return true;
            }

            return false;
        }

        //THESE MAY NEED TO BE MOVED
        public void Broadcast(String Message)
        {
            RCON.addRCON("sayraw " + Message, 0);
        }

        public void Tell(String Message, Player Target)
        {
            RCON.addRCON("tell " + Target.getClientNum() + " " + Message + "^7", 0);
        }

        public void Kick(String Message, Player Target)
        {
            RCON.addRCON("clientkick " + Target.getClientNum() + " \"" + Message + "^7\"", 0);
        }

        public void Ban(String Message, Player Target, Player Origin)
        {
            RCON.addRCON("tempbanclient " + Target.getClientNum() + " \"" + Message + "^7\"", 0);
            if (Origin != null)
            {
                Target.setLevel(Player.Permission.Banned);
                Ban newBan = new Ban(Target.getLastO(), Target.getID(), Origin.getID());
                Bans.Add(newBan);
                DB.addBan(newBan);
                DB.updatePlayer(Target);
            }
        }

        public bool Unban(String GUID)
        {
            foreach (Ban B in Bans)
            {
                if (B.getID() == GUID)
                {
                    DB.removeBan(GUID);
                    Bans.Remove(B);
                    Player P = DB.getPlayer(GUID, 0);
                    P.setLevel(Player.Permission.User);
                    DB.updatePlayer(P);
                    return true;
                }
            }
            return false;
        }


        public void fastRestart(int delay)
        {
            Utilities.Wait(delay);
            RCON.addRCON("fast_restart", 0);
        }

        public void mapRotate(int delay)
        {
            Utilities.Wait(delay);
            RCON.addRCON("map_rotate", 0);
        }

        public void tempBan(String Message, Player Target)
        {
            RCON.addRCON("tempbanclient " + Target.getClientNum() + " \"" + Message + "\"", 0);
        }
        
        public void mapRotate()
        {
            RCON.addRCON("map_rotate", 0);
        }

        public void Map(String map)
        {
            RCON.addRCON("map " + map, 0);
        }
        //END

        //THIS IS BAD BECAUSE WE DON"T WANT EVERYONE TO HAVE ACCESS :/
        public String getPassword()
        {
            return rcon_pass;
        }

        private void initMaps()
        {
            file mapfile = new file("config\\maps.cfg");
            String[] _maps = mapfile.readAll();
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
            file messageCFG = new file("config\\messages.cfg");
            String[] lines = messageCFG.readAll();

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
        }

        private void initRules()
        {
            file ruleFile = new file("config\\rules.cfg");
            String[] _rules = ruleFile.readAll();
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
        }

        private void initCommands()
        {
            // Something like *COMMAND* | NAME | HELP MSG | ALIAS | NEEDED PERMISSION | # OF REQUIRED ARGS | HAS TARGET |

            commands = new List<Command>();

            if(owner == null)
                commands.Add(new Owner("owner", "claim ownership of the server", "owner", Player.Permission.User, 0, false));

            commands.Add(new Kick("kick", "kick a player by name. syntax: !kick <player> <reason>.", "k", Player.Permission.Moderator, 2, true));
            commands.Add(new Say("say", "broadcast message to all players. syntax: !say <message>.", "s", Player.Permission.Moderator, 1, false));
            commands.Add(new TempBan("tempban", "temporarily ban a player for 1 hour. syntax: !tempban <player> <reason>.", "tb", Player.Permission.Moderator, 2, true));
            commands.Add(new SBan("ban", "permanently ban a player from the server. syntax: !ban <player> <reason>", "b", Player.Permission.SeniorAdmin, 2, true));
            commands.Add(new WhoAmI("whoami", "give information about yourself. syntax: !whoami.", "who", Player.Permission.User, 0, false));
            commands.Add(new List("list", "list active clients syntax: !list.", "l", Player.Permission.Moderator, 0, false));
            commands.Add(new Help("help", "list all available commands. syntax: !help.", "l", Player.Permission.User, 0, false));
            commands.Add(new FastRestart("fastrestart", "fast restart current map. syntax: !fastrestart.", "fr", Player.Permission.Moderator, 0, false));
            commands.Add(new MapRotate("maprotate", "cycle to the next map in rotation. syntax: !maprotate.", "mr", Player.Permission.Administrator, 0, false));
            commands.Add(new SetLevel("setlevel", "set player to specified administration level. syntax: !setlevel <player> <level>.", "sl", Player.Permission.Owner, 2, true));
            commands.Add(new Usage("usage", "get current application memory usage. syntax: !usage.", "u", Player.Permission.Moderator, 0, false));
            commands.Add(new Uptime("uptime", "get current application running time. syntax: !uptime.", "up", Player.Permission.Moderator, 0, false));
            commands.Add(new Warn("warn", "warn player for infringing rules syntax: !warn <player> <reason>.", "w", Player.Permission.Moderator, 2, true));
            commands.Add(new WarnClear("warnclear", "remove all warning for a player syntax: !warnclear <player>.", "wc", Player.Permission.Administrator, 1, true));
            commands.Add(new Unban("unban", "unban player by guid. syntax: !unban <guid>.", "ub", Player.Permission.Administrator, 1, false));
            commands.Add(new Admins("admins", "list currently connected admins. syntax: !admins.", "a", Player.Permission.User, 0, false));
            commands.Add(new Wisdom("wisdom", "get a random wisdom quote. syntax: !wisdom", "w", Player.Permission.Administrator, 0, false));
            commands.Add(new MapCMD("map", "change to specified map. syntax: !map", "m", Player.Permission.Administrator, 1, false));
            commands.Add(new Find("find", "find player in database. syntax: !find <player>", "f", Player.Permission.Administrator, 1, false));
            commands.Add(new Rules("rules", "list server rules. syntax: !rules", "r", Player.Permission.User, 0, false));

            /*
            commands.Add(new commands { command = "stats", desc = "view your server stats.", requiredPer = 0 });
            commands.Add(new commands { command = "speed", desc = "change player speed. syntax: !speed <number>", requiredPer = 3 });
            commands.Add(new commands { command = "gravity", desc = "change game gravity. syntax: !gravity <number>", requiredPer = 3 });

            commands.Add(new commands { command = "version", desc = "view current app version.", requiredPer = 0 });*/
        }

        //Objects
        public Log Log;
        public RCON RCON;
        public Database DB;
        public List<Ban> Bans;
        public Player owner;
        public List<Map> maps;
        public List<String> rules;
        public Queue<Event> events;

        //Info
        private String IP;
        private int Port;
        private String hostname;
        private int clientnum;
        private string rcon_pass;
        private List<Player> players;
        private List<Command> commands;
        private List<String> messages;
        private int messageTime;
        private TimeSpan lastMessage;
        private int nextMessage;
        private int errors = 0;
     
        //Log stuff
        private String Basepath;
        private String Mod;
        private String logPath;
        private file logFile;
    }
}
