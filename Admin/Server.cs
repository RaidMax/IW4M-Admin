using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;


namespace IW4MAdmin
{
    class Server
    {
        const int FLOOD_TIMEOUT = 300;

        public Server(string address, int port, string password, int H, int PID)
        {
            this.PID = PID;
            Handle = H;
            IP = address;
            Port = port;
            clientnum = 0;
            RCON = new RCON(this);
            logFile = new file("admin_" + port + ".log", true);
#if DEBUG
            Log = new Log(logFile, Log.Level.Debug, port);
#else
            Log = new Log(logFile, Log.Level.Production, port);
#endif
            players = new List<Player>(new Player[18]);
            clientDB = new ClientsDB("clients.rm");
            statDB = new StatsDB("stats_" + port + ".rm");
            aliasDB = new AliasesDB("aliases.rm");
            Bans = clientDB.getBans();
            owner = clientDB.getOwner();
            events = new Queue<Event>();
            HB = new Heartbeat(this);
            Macros = new Dictionary<String, Object>();
            Reports = new List<Report>();
            Skills = new Moserware.TrueSkill();
            statusPlayers = new Dictionary<string, Player>();
            chatHistory = new List<Chat>();
            playerHistory = new Queue<pHistory>();
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

        public void getAliases(List<Player> returnPlayers, Player Origin)
        {  
            if (Origin == null)
                return;

            List<Aliases> aliasAliases = new List<Aliases>();
            Aliases currentAliases = aliasDB.getPlayer(Origin.getDBID());

            if (currentAliases == null)
            {
                Log.Write("No aliases found for " + Origin.getName(), Log.Level.Debug);
                return;
            }

            foreach (String IP in currentAliases.getIPS())
            {
                    List<Aliases> tmp = aliasDB.getPlayer(IP);
                    if (tmp != null)
                        aliasAliases = tmp;

                    foreach (Aliases a in aliasAliases)
                    {
                        if (a == null)
                            continue;

                        Player aliasPlayer = clientDB.getPlayer(a.getNumber());

                        if (aliasPlayer != null)
                        {
                            aliasPlayer.Alias = a;

                            if (returnPlayers.Exists(p => p.getDBID() == aliasPlayer.getDBID()) == false)
                            {
                                returnPlayers.Add(aliasPlayer);
                                getAliases(returnPlayers, aliasPlayer);
                            }
                        }
                    }
            }           
        }

        bool checkClientStatus(Player P)
        {
          /*  RCON.addRCON("admin_lastevent status;" + P.getID() + ";0;clean");
            Utilities.Wait(0.5); // give it time to update
            String[] Status = RCON.addRCON("whoisdirty");

            if (Status != null)
            {
                String GUID = Utilities.stripColors(Status[1].Split(new char[] { '\"' })[3]);
            }
            */
  
            return true;
        }
     
        //Add player object p to `players` list
        public bool addPlayer(Player P)
        {
            if (P.getClientNum() < 0 || P.getClientNum() > (players.Count-1)) // invalid index
                return false;

            if (players[P.getClientNum()] != null && players[P.getClientNum()].getID() == P.getID()) // if someone has left and a new person has taken their spot between polls
                return true;

            Log.Write("Client slot #" + P.getClientNum() + " now reserved", Log.Level.Debug);

                
#if DEBUG == false
            try
#endif
            {
                Player NewPlayer = clientDB.getPlayer(P.getID(), P.getClientNum());

                if (NewPlayer == null) // first time connecting
                {
                    Log.Write("Client slot #" + P.getClientNum() + " first time connecting", Log.Level.All);
                    clientDB.addPlayer(P);
                    NewPlayer = clientDB.getPlayer(P.getID(), P.getClientNum());
                    aliasDB.addPlayer(new Aliases(NewPlayer.getDBID(), NewPlayer.getName(), NewPlayer.getIP()));
                    statDB.addPlayer(NewPlayer);
                }

                NewPlayer.updateName(P.getName().Trim());
    
                NewPlayer.stats = statDB.getStats(NewPlayer.getDBID());
                NewPlayer.Alias = aliasDB.getPlayer(NewPlayer.getDBID());

                if (NewPlayer.Alias == null)
                {
                    aliasDB.addPlayer(new Aliases(NewPlayer.getDBID(), NewPlayer.getName(), NewPlayer.getIP()));
                    NewPlayer.Alias = aliasDB.getPlayer(NewPlayer.getDBID());
                }
                
                // try not to crash if no stats!

                if (P.lastEvent == null || P.lastEvent.Owner == null)
                    NewPlayer.lastEvent = new Event(Event.GType.Say, null, NewPlayer, null, this); // this is messy but its throwing an error when they've started it too late
                else
                    NewPlayer.lastEvent = P.lastEvent;

           
                // lets check aliases 
                if ((NewPlayer.Alias.getNames().Find(m => m.Equals(P.getName()))) == null || NewPlayer.getName() == null || NewPlayer.getName() == String.Empty) 
                {
                    NewPlayer.updateName(P.getName().Trim());
                    NewPlayer.Alias.addName(NewPlayer.getName());
                }
               
                // and ips
                if (NewPlayer.Alias.getIPS().Find(i => i.Equals(P.getIP())) == null || P.getIP() == null || P.getIP() == String.Empty)
                {
                    NewPlayer.Alias.addIP(P.getIP());
                }

                NewPlayer.updateIP(P.getIP());

                aliasDB.updatePlayer(NewPlayer.Alias);
                clientDB.updatePlayer(NewPlayer);

                if (NewPlayer.getLevel() == Player.Permission.Banned) // their guid is already banned so no need to check aliases
                {
                    String Message;

                    Log.Write("Banned client " + P.getName() + " trying to connect...", Log.Level.Debug);

                    if (NewPlayer.getLastO() != null)
                        Message = "^7Player Kicked: Previously banned for ^5" + NewPlayer.getLastO() + " ^7(appeal at " + Website + ")";
                    else
                        Message = "Player Kicked: Previous Ban";

                    NewPlayer.Kick(Message);

                    if (players[NewPlayer.getClientNum()] != null)
                    {
                        lock (players)
                        {
                            players[NewPlayer.getClientNum()] = null;
                        }
                    }

                    return true;
                }

                List<Player> newPlayerAliases = new List<Player>();
                getAliases(newPlayerAliases, NewPlayer);

                foreach (Player aP in newPlayerAliases) // lets check their aliases
                {
                    if (aP == null)
                        continue;

                    if (aP.getLevel() == Player.Permission.Flagged)
                        NewPlayer.setLevel(Player.Permission.Flagged);

                    Ban B = isBanned(aP);

                    if (B != null)
                    {
                        Log.Write(String.Format("Banned client {0} is connecting with new alias {1}", aP.getName(), NewPlayer.getName()), Log.Level.Debug);
                        NewPlayer.LastOffense = String.Format("Evading ( {0} )", aP.getName());

                        if (B.getReason() != null)
                            NewPlayer.Ban("^7Previously Banned: ^5" + B.getReason() + " ^7(appeal at " + Website  + ")", NewPlayer);
                        else
                            NewPlayer.Ban("^7Previous Ban", NewPlayer);

                        lock (players)
                        {
                            if (players[NewPlayer.getClientNum()] != null)
                                players[NewPlayer.getClientNum()] = null;
                        }
                        return true;
                    }
                }

                //finally lets check their clean status :>
                checkClientStatus(NewPlayer);

                lock (players)
                {
                    players[NewPlayer.getClientNum()] = null; // just in case we have shit in the way
                    players[NewPlayer.getClientNum()] = NewPlayer;
                }
#if DEBUG == FALSE
                    NewPlayer.Tell("Welcome ^5" + NewPlayer.getName() + " ^7this is your ^5" + Utilities.timesConnected(NewPlayer.getConnections()) + " ^7time connecting!");
#endif
                Log.Write("Client " + NewPlayer.getName() + " connecting...", Log.Level.Debug); // they're clean

                while (chatHistory.Count > Math.Ceiling((double)clientnum / 2))
                    chatHistory.RemoveAt(0);
                chatHistory.Add(new Chat(NewPlayer, "<i>CONNECTED</i>", DateTime.Now));

                if (NewPlayer.getLevel() == Player.Permission.Flagged)
                    ToAdmins("^1NOTICE: ^7Flagged player ^5" + NewPlayer.getName() + "^7 has joined!");

                if (NewPlayer.getLevel() > Player.Permission.Moderator)
                    NewPlayer.Tell("There are ^5" + Reports.Count + " ^7recent reports!");

                if (NewPlayer.stats == null) // there seems to be an issue with stats with multiple servers. I think this should fix it
                { 
                    statDB.addPlayer(NewPlayer);
                    NewPlayer.stats = statDB.getStats(NewPlayer.getDBID());
                }

                return true;
            }
#if DEBUG == false
            catch (Exception E)
            {
                Log.Write("Unable to add player " + P.getName() + " - " + E.Message, Log.Level.Debug);
                return false;
            }
#endif
        }

        //Remove player by CLIENT NUMBER
        public bool removePlayer(int cNum)
        {
            if (cNum >= 0 && cNum < players.Count)
            {
                if (players[cNum] == null)
                {
                    //Log.Write("Error - Disconnecting client slot is already empty!", Log.Level.Debug);
                    return false;
                }

                Player Leaving = players[cNum];
                Leaving.Connections++;
                clientDB.updatePlayer(Leaving);
                statDB.updatePlayer(Leaving);

                Log.Write("Client at " + cNum + " disconnecting...", Log.Level.Debug);
                lock (players)
                {
                    players[cNum] = null;
                }
                return true;
            }

            else
            {
                Log.Write("Error - Client disconnecting has an invalid client index!", Log.Level.Debug);
                return false;
            }
        }

        //Another version of client from line, written for the line created by a kill or death event
        public Player clientFromEventLine(String[] L, int cIDPos)
        {
            if (L.Length < cIDPos)
            {
                Log.Write("Line sent for client creation is not long enough!", Log.Level.Debug);
                return null;
            }

            int pID = -2; // apparently falling = -1 cID so i can't use it now
            int.TryParse(L[cIDPos].Trim(), out pID);

            if (pID == -1) // special case similar to mod_suicide
                int.TryParse(L[2], out pID);

            if (pID < 0 || pID > 17)
            {
                Log.Write("Error event player index " + pID + " is out of bounds!", Log.Level.Debug);
                Log.Write("Offending line -- " + String.Join(";", L), Log.Level.Debug);
                return null;
            }

            else
            {
                Player P = null;
                try 
                { 
                    P = players[pID];
                    return P;
                }
                catch (Exception) 
                { 
                    Log.Write("Client index is invalid - " + pID, Log.Level.Debug);
                    Log.Write(L.ToString(), Log.Level.Debug);
                    return null;
                }
            } 
        }

        public Player clientFromName(String pName)
        {
            lock (players)
            {
                foreach (var P in players)
                {
                    if (P != null && P.getName().ToLower().Contains(pName.ToLower()))
                        return P;
                }
            }

            return null;
        }

        //Check ban list for every banned player and return ban if match is found 
         public Ban isBanned(Player C)
         {
            
                 if (C.getLevel() == Player.Permission.Banned)
                    return Bans.Find(p => p.getID().Equals(C.getID()));

                 foreach (Ban B in Bans)
                 {
                     if (B.getID().Length < 5 || B.getIP().Length < 5)
                         continue;

                     if (B.getID() == null || C.getID() == null)
                         continue;

                     if (B.getID() == C.getID())
                         return B;

                     if (B.getIP() == null || C.getIP() == null)
                         continue;

                     if (C.getIP() == B.getIP())
                         return B;
                 }

            return null;
         }

        //Procses requested command correlating to an event
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

                if (C.getName() == "stats" && Args.Length == 1)
                    E.Target = E.Origin;

                if (Args[0] == String.Empty)
                    return C;


                if (Args[0][0] == '@') // user specifying target by database ID
                {
                    int dbID = -1;
                    int.TryParse(Args[0].Substring(1, Args[0].Length-1), out dbID);
                    Player found = E.Owner.clientDB.getPlayer(dbID);
                    if (found != null)
                    {
                        E.Target = found;
                        E.Target.lastEvent = E;
                        E.Owner = this;
                    }
                }

                else if(Args[0].Length < 3 && cNum > -1 && cNum < 18) // user specifying target by client num
                {
                    if (players[cNum] != null)
                        E.Target = players[cNum];
                }

                else
                    E.Target = clientFromName(Args[0]);

                if (E.Target == null)
                {
                    E.Origin.Tell("Unable to find specified player.");
                    return null;
                }
            }
            return C;
        }

        //push a new event into the queue
        private void addEvent(Event E)
        {
            events.Enqueue(E);
        }
        

        //process new event every 50 milliseconds
        private void manageEventQueue()
        {
            while (isRunning)
            {
                if (events.Count > 0)
                {
                    processEvent(events.Peek());
                    events.Dequeue();
                }
                Utilities.Wait(0.05);
            }
        }

        public void executeCommand(String CMD)
        {
            if (CMD.ToLower() == "map_restart" || CMD.ToLower() == "map_rotate")
                return;

            else if (CMD.ToLower().Substring(0, 4) == "map ")
            {
                backupRotation = getDvar("sv_mapRotation").current;
                backupTimeLimit = Convert.ToInt32(getDvar("scr_" + Gametype + "_timelimit").current);
                Utilities.executeCommand(PID, "sv_maprotation map " + CMD.ToLower().Substring(4, CMD.Length-4));
                Utilities.executeCommand(PID, "scr_" + Gametype + "_timelimit 0.001");
                Utilities.Wait(1);
                Utilities.executeCommand(PID, "scr_" + Gametype + "_timelimit " + backupTimeLimit);
            }

            else
                Utilities.executeCommand(PID, CMD);
                
        }

        private dvar getDvar(String DvarName)
        {
            return Utilities.getDvarValue(PID, DvarName);
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        //Starts the monitoring process
        public void Monitor()
        {
            isRunning = true;

#if DEBUG
          /*  Random rnd = new Random();
            DateTime testTOD = DateTime.Now;
            while (playerHistory.Count < 144)
            {
                playerHistory.Enqueue(new pHistory(testTOD, rnd.Next(14, 19)));
                 testTOD = testTOD.AddMinutes(5);
            }

            Console.WriteLine("There are " + playerHistory.Count + " player counts");*/
#endif

            //Handles new rcon requests in a fashionable manner
            Thread RCONQueue = new Thread(new ThreadStart(RCON.ManageRCONQueue));
            RCONQueue.Start();

            if (!intializeBasics())
            {
                Log.Write("Stopping " + Port + " due to uncorrectable errors (check log)" + logPath, Log.Level.Production);
                isRunning = false;
                Utilities.Wait(10);  
                return;
            }



#if DEBUG
            //Thread to handle polling server for IP's
            Thread statusUpdate = new Thread(new ThreadStart(pollServer));
            statusUpdate.Start();
#endif
            //Handles new events in a fashionable manner
            Thread eventQueue = new Thread(new ThreadStart(manageEventQueue));
            eventQueue.Start();

            int timesFailed = 0;
            long l_size = -1;
            bool checkedForOutdate = false;
            String[] lines = new String[8];
            String[] oldLines = new String[8];
            DateTime start = DateTime.Now;
            DateTime playerCountStart = DateTime.Now;
            DateTime lastCount = DateTime.Now;

#if DEBUG == false
            Broadcast("IW4M Admin is now ^2ONLINE");
#endif

            while (isRunning)
            {
#if DEBUG == false
               try
#endif
                {
                    lastMessage = DateTime.Now - start;
                    lastCount = DateTime.Now;

                    if ((lastCount - playerCountStart).TotalMinutes > 4)
                    {
                        while (playerHistory.Count > 144 ) // 12 times a minute for 12 hours
                            playerHistory.Dequeue();
                        playerHistory.Enqueue(new pHistory(lastCount, clientnum));
                        playerCountStart = DateTime.Now;
                    }

                    if(lastMessage.TotalSeconds > messageTime && messages.Count > 0)
                    {
                        initMacros(); // somethings dynamically change so we have to re-init the dictionary
                        Broadcast(Utilities.processMacro(Macros, messages[nextMessage]));
                        if (nextMessage == (messages.Count - 1))
                            nextMessage = 0;
                        else
                            nextMessage++;
                        start = DateTime.Now;
                        if (timesFailed <= 3)
                            HB.Send();

                        String checkVer = new Connection("http://raidmax.org/IW4M/Admin/version.php").Read();
                        double checkVerNum;
                        double.TryParse(checkVer, out checkVerNum);
                        if (checkVerNum != Program.Version && checkVerNum != 0 && !checkedForOutdate)
                        {
                            messages.Add("^5IW4M Admin ^7is outdated. Please ^5update ^7to version " + checkVerNum);
                            checkedForOutdate = true;
                        }
                        
                    }
#if DEBUG == false
                    if ((DateTime.Now - lastPoll).Milliseconds > 750)
                    {
                        int numberRead = 0;
                        int activeClients = 0;

                        for (int i = 0; i < players.Count; i++)
                        {
                            Byte[] buff = new Byte[681872]; // struct size ( 0.68MB :( )
                            ReadProcessMemory((int)Handle, 0x31D9390 + (buff.Length)*(i), buff, buff.Length, ref numberRead); // svs_clients start + current client

                            client_s eachClient = (client_s)Helpers.ReadStruct<client_s>(buff);
                            if (eachClient.isBot == 1)
                                continue;

                            if (eachClient.state == 0)
                                removePlayer(i);
                            else if (eachClient.state > 1)
                                addPlayer(new Player(Utilities.stripColors(Utilities.cleanChars(eachClient.name)), eachClient.steamid.ToString("x16"), i, 0, i, null, 0, Helpers.NET_AdrToString(eachClient.adr).Split(':')[0]));
                            if (eachClient.state > 2)
                                activeClients++;
                        }

                        lastPoll = DateTime.Now;
                        clientnum = activeClients;
                    }
#endif

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

                                if (lines[count].Length < 10) // its not a needed line 
                                    continue;

                                else
                                {
                                    string[] game_event = lines[count].Split(';');
                                    Event event_ = Event.requestEvent(game_event, this);
                                    if (event_ != null)
                                    {
                                        if (event_.Origin == null)
                                            continue;

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
                    Thread.Sleep(250);
                }
#if DEBUG == false
                catch (Exception E)
                {
                    Log.Write("Something unexpected occured. Hopefully we can ignore it - " + E.Message + " @"  + Utilities.GetLineNumber(E), Log.Level.All);
                    continue;
                }
#endif

            }
            isRunning = false;
            RCONQueue.Abort();
            eventQueue.Abort();
        }
#if DEBUG
        private void pollServer()
        {
            int timesFailed = 0;
            Dictionary<String, Player> toCheck = new Dictionary<String, Player>();
            while (isRunning)
            {
                String[] Response = RCON.addRCON("status");
                if (Response != null)
                    toCheck = Utilities.playersFromStatus(Response);
           
                if (toCheck != null)
                {
                    lastPoll = DateTime.Now;
                    timesFailed = 0;

                    if (toCheck != statusPlayers)
                    {
                        List<Player> toRemove = new List<Player>();
                        lock (players)
                        {
                            foreach (Player P in players)
                            {
                                if (P == null)
                                    continue;

                                Player Matching;
                                toCheck.TryGetValue(P.getID(), out Matching);
                                if (Matching == null) // they are no longer with us
                                    toRemove.Add(P);
                            }

                            foreach (Player Removing in toRemove) // cuz cant modify collections
                                removePlayer(Removing.getClientNum());
                        }

                        foreach (var P in toCheck.Values)
                        {
                            if (P == null)
                            {
                                Log.Write("Null player found in toCheck", Log.Level.Debug);
                                continue;
                            }

                            if (!addPlayer(P))
                                Log.Write("Error adding " + P.getName() + " at client slot #" + P.getClientNum(), Log.Level.Debug);
                        }

                        lock (statusPlayers)
                        {
                            statusPlayers = toCheck;
                        }
                    }

                    clientnum = statusPlayers.Count;
                }

                else
                {
                    timesFailed++;
                    Log.Write("Server appears to be offline - " + timesFailed, Log.Level.Debug);

                    if (timesFailed >= 4)
                    {
                        Log.Write("Max offline attempts reached. Reinitializing RCON connection.", Log.Level.Debug);
                        RCON.Reset();
                    }
                }

                Utilities.Wait(15);
            }
        }
#endif
        private bool intializeBasics()
        {
           try
           {
               // basic info dvars
                hostname         = Utilities.stripColors(getDvar("sv_hostname").current);
                mapname          = getDvar("mapname").current;
                IW_Ver           = getDvar("shortversion").current;
                maxClients       = Convert.ToInt32(getDvar("party_maxplayers").current);
                Gametype         = getDvar("g_gametype").current;

               // important log variables
                Basepath         = getDvar("fs_basepath").current;
                Mod              = getDvar("fs_game").current;
                logPath          = getDvar("g_log").current;
                //int logSync = Convert.ToInt32(getDvar("g_logSync").current);   

               if (Mod == String.Empty)
                   logPath = Basepath + '\\' + "m2demo" + '\\' + logPath;
               else
                   logPath = Basepath + '\\' + Mod + '\\' + logPath;

               if (!File.Exists(logPath))
               {
                   Log.Write("Gamelog does not exist!", Log.Level.All);
                   return false;
               }

               logFile = new file(logPath);
               Log.Write("Log file is " + logPath, Log.Level.Production);
               Log.Write("Now monitoring " + this.getName(), Log.Level.Production);
               return true;
            }
            catch (Exception E)
            {
                Log.Write("Error during initialization - " + E.Message +"--" + E.StackTrace, Log.Level.All);
                return false;
            }
        }

        //Process any server event
        public bool processEvent(Event E)
        {
            if (E.Type == Event.GType.Connect)
            {
                return true;
            }

            if (E.Type == Event.GType.Disconnect)
            {
                if (E.Origin == null)
                {
                    Log.Write("Disconnect event triggered, but no origin found.", Log.Level.Debug);
                    return false;
                }

                while (chatHistory.Count > Math.Ceiling(((double)clientnum - 1) / 2))
                    chatHistory.RemoveAt(0);
                chatHistory.Add(new Chat(E.Origin, "<i>DISCONNECTED</i>", DateTime.Now));

                removePlayer(E.Origin.getClientNum());        
                return true;
            }

            if (E.Type == Event.GType.Kill)
            {
                if (E.Origin == null)
                {
                    Log.Write("Kill event triggered, but no origin found!", Log.Level.Debug);
                    return false;
                }

                if (E.Target == null)
                {
                    Log.Write("Kill event triggered, but no target found!", Log.Level.Debug);
                    return false;
                }

                if (E.Origin.stats == null)
                {
                    Log.Write("Kill event triggered, but no stats found for origin!", Log.Level.Debug);
                    return false;
                }

                if (E.Target.stats == null)
                {
                    Log.Write("Kill event triggered, but no stats found for target!", Log.Level.Debug);
                    return false;
                }

                if (E.Origin != E.Target)
                {
                    E.Origin.stats.Kills += 1;
                    E.Origin.stats.updateKDR();

                    E.Target.stats.Deaths += 1;
                    E.Target.stats.updateKDR();

                    Skills.updateNewSkill(E.Origin, E.Target);
                    statDB.updatePlayer(E.Origin);
                    statDB.updatePlayer(E.Target);

                    totalKills++;
                    Log.Write(E.Origin.getName() + " killed " + E.Target.getName() + " with a " + E.Data, Log.Level.Debug);
                }

                else // suicide/falling
                {
                    E.Origin.stats.Deaths++;
                    E.Origin.stats.updateKDR();
                    statDB.updatePlayer(E.Origin);
                    Log.Write(E.Origin.getName() + " suicided...", Log.Level.Debug);
                }
            }

            if (E.Type == Event.GType.Say)
            {

                if (E.Data.Length < 2) // ITS A LIE!
                    return false;

                if (E.Origin == null)
                {
                    Log.Write("Say event triggered, but no origin found! - " + E.Data, Log.Level.Debug);
                    return false;
                }

                Log.Write("Message from " + E.Origin.getName() + ": " + E.Data, Log.Level.Debug);

                if (E.Owner == null)
                {
                    Log.Write("Say event does not have an owner!", Log.Level.Debug);
                    return false;
                }

                if (E.Data.Substring(0, 1) != "!") // Not a command so who gives an F?
                {
                    E.Data = Utilities.stripColors(Utilities.cleanChars(E.Data));
                    if (E.Data.Length > 50)
                        E.Data = E.Data.Substring(0, 50) + "...";
                    while (chatHistory.Count > Math.Ceiling((double)clientnum/2))
                        chatHistory.RemoveAt(0);

                    chatHistory.Add(new Chat(E.Origin, E.Data, DateTime.Now));

                    return true;
                }

                Command C = E.isValidCMD(commands);

                if (C != null)
                {
                    C = processCommand(E, C);
                    if (C != null)
                    {
                        if (C.needsTarget() && E.Target == null)
                        {
                            Log.Write("Requested event requiring target does not have a target!", Log.Level.Debug);
                            return false;
                        }
                        C.Execute(E);
                        return true;
                    }

                    else
                    {
                        Log.Write("Player didn't properly enter command - " + E.Origin.getName(), Log.Level.Debug);
                        return true;
                    }
                }

                else
                    E.Origin.Tell("You entered an invalid command!");

                return true;
            }

            if (E.Type == Event.GType.MapChange)
            {
                Log.Write("New map loaded - " + clientnum + " active players", Log.Level.Debug);

                Dictionary<String, String> infoResponseDict = new Dictionary<String, String>();
                String[] infoResponse = E.Data.Split('\\');

                for (int i = 0; i < infoResponse.Length; i++)
                {
                    if (i % 2 == 0 || infoResponse[i] == String.Empty)
                        continue;
                    infoResponseDict.Add(infoResponse[i], infoResponse[i + 1]);
                }

                String newMapName = null;
                infoResponseDict.TryGetValue("mapname", out newMapName);

                if (newMapName != null)
                {
                    try
                    {
                        Map newMap = maps.Find(m => m.Name.Equals(newMapName));
                        mapname = newMap.Alias;
                    }

                    catch (Exception)
                    {
                        Log.Write(mapname + " doesn't appear to be in the maps.cfg", Log.Level.Debug);
                    }
                }

                else
                    Log.Write("Could not get new mapname from InitGame line!", Log.Level.Debug);

                return true;
            }

            if (E.Type == Event.GType.MapEnd)
            {
                Log.Write("Game ending...", Log.Level.Debug);
                foreach (Player P in players)
                {
                    if (P == null || P.stats == null)
                        continue;
                    statDB.updatePlayer(P);
                    Log.Write("Updated stats for client " + P.getDBID(), Log.Level.Debug);
                }
                return true;
            }

            return false;
        }

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
            RCON.addRCON("sayraw " + Message);
        }

        public void Tell(String Message, Player Target)
        {
            if (Target.getClientNum() > -1)
                RCON.addRCON("tellraw " + Target.getClientNum() + " " + Message + "^7"); // I fixed tellraw :>
        }

        public void Kick(String Message, Player Target)
        {
            if (Target.getClientNum() > -1)
                RCON.addRCON("clientkick " + Target.getClientNum() + " \"" + Message + "^7\"");
        }

        public void Ban(String Message, Player Target, Player Origin)
        {
            if (Target.getClientNum() > -1)
                RCON.addRCON("tempbanclient " + Target.getClientNum() + " \"" + Message + "^7\"");

            if (Origin != null)
            {
                Target.setLevel(Player.Permission.Banned);
                Ban newBan = new Ban(Target.getLastO(), Target.getID(), Origin.getID(), DateTime.Now, Target.getIP());
                Bans.Add(newBan);
                clientDB.addBan(newBan);
                clientDB.updatePlayer(Target);
                lock (Reports) // threading seems to do something weird here
                {
                    List<Report> toRemove = new List<Report>();
                    foreach (Report R in Reports)
                    {
                        if (R.Target.getID() == Target.getID())
                            toRemove.Add(R);
                    }

                    foreach (Report R in toRemove)
                    {
                        Reports.Remove(R);
                        Log.Write("Removing report for banned GUID -- " + R.Origin.getID(), Log.Level.Debug);
                    }
                }
            }
        }

        public bool Unban(String GUID, Player Target)
        {
            foreach (Ban B in Bans)
            {
                if (B.getID() == Target.getID())
                {
                    clientDB.removeBan(Target.getID(), Target.getIP());

                    Player P = clientDB.getPlayer(Target.getID(), -1);
                    P.setLevel(Player.Permission.User);
                    clientDB.updatePlayer(P);

                    Bans = clientDB.getBans();
                    return true;
                }
            }
            return false;
        }


        public void fastRestart(int delay)
        {
            Utilities.Wait(delay);
            RCON.addRCON("fast_restart");
        }

        public void mapRotate(int delay)
        {
            Utilities.Wait(delay);
            executeCommand("map_rotate");
        }

        public void tempBan(String Message, Player Target)
        {
            RCON.addRCON("tempbanclient " + Target.getClientNum() + " \"" + Message + "\"");
        }
        
        public void mapRotate()
        {
            mapRotate(0);
        }

        public void Map(String mapName)
        {
            executeCommand("map " + mapName);
        }

        public String Wisdom()
        {
            String Quote = new Connection("http://www.iheartquotes.com/api/v1/random?max_lines=1&max_characters=200").Read();
            return Utilities.removeNastyChars(Quote);
        }

        public void ToAdmins(String message)
        {
            lock (players) // threading can modify list while we do this
            {
                foreach (Player P in players)
                {
                    if (P == null)
                        continue;

                    if (P.getLevel() > Player.Permission.Flagged)
                    {
                        P.Alert();
                        P.Tell(message);
                    }
                }
            }
        }

        public void Alert(Player P)
        {
            RCON.addRCON("admin_lastevent alert;" + P.getID() + ";0;mp_killstreak_nuclearstrike");
        }

        public void webChat(Player P, String Message)
        {
            DateTime requestTime = DateTime.Now;

            if ((requestTime - lastWebChat).TotalSeconds > 1)
            {
                Broadcast("^1[WEBCHAT] ^5" + P.getName() + "^7 - " + Message);
                while (chatHistory.Count > Math.Ceiling((double)clientnum / 2))
                    chatHistory.RemoveAt(0);

                if (Message.Length > 50)
                    Message = Message.Substring(0, 50) + "...";

                chatHistory.Add(new Chat(P, Utilities.stripColors(Message), DateTime.Now));
                lastWebChat = DateTime.Now;
            }
        }

        //END

        public String getPassword()
        {
            return rcon_pass;
        }

        private void initMacros()
        {
            Macros = new Dictionary<String, Object>();
            Macros.Add("WISDOM", Wisdom());
            Macros.Add("TOTALPLAYERS", clientDB.totalPlayers());
            Macros.Add("TOTALKILLS", totalKills);
            Macros.Add("VERSION", IW4MAdmin.Program.Version);     
        }

        private void initMaps()
        {
            maps = new List<Map>();

            file mapfile = new file("config\\maps.cfg");
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

            file messageCFG = new file("config\\messages.cfg");
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
            if (Program.Version != Program.latestVersion && Program.latestVersion != 0)
               messages.Add("^5IW4M Admin ^7is outdated. Please ^5update ^7to version " + Program.latestVersion);
        }

        private void initRules()
        {
            rules = new List<String>();

            file ruleFile = new file("config\\rules.cfg");
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
            commands.Add(new Help("help", "list all available commands. syntax: !help.", "h", Player.Permission.User, 0, false));
            commands.Add(new FastRestart("fastrestart", "fast restart current map. syntax: !fastrestart.", "fr", Player.Permission.Moderator, 0, false));
            commands.Add(new MapRotate("maprotate", "cycle to the next map in rotation. syntax: !maprotate.", "mr", Player.Permission.Administrator, 0, false));
            commands.Add(new SetLevel("setlevel", "set player to specified administration level. syntax: !setlevel <player> <level>.", "sl", Player.Permission.Owner, 2, true));
            commands.Add(new Usage("usage", "get current application memory usage. syntax: !usage.", "us", Player.Permission.Moderator, 0, false));
            commands.Add(new Uptime("uptime", "get current application running time. syntax: !uptime.", "up", Player.Permission.Moderator, 0, false));
            commands.Add(new Warn("warn", "warn player for infringing rules syntax: !warn <player> <reason>.", "w", Player.Permission.Moderator, 2, true));
            commands.Add(new WarnClear("warnclear", "remove all warning for a player syntax: !warnclear <player>.", "wc", Player.Permission.Administrator, 1, true));
            commands.Add(new Unban("unban", "unban player by database id. syntax: !unban @<id>.", "ub", Player.Permission.SeniorAdmin, 1, true));
            commands.Add(new Admins("admins", "list currently connected admins. syntax: !admins.", "a", Player.Permission.User, 0, false));
            commands.Add(new Wisdom("wisdom", "get a random wisdom quote. syntax: !wisdom", "w", Player.Permission.Administrator, 0, false));
            commands.Add(new MapCMD("map", "change to specified map. syntax: !map", "m", Player.Permission.Administrator, 1, false));
            commands.Add(new Find("find", "find player in database. syntax: !find <player>", "f", Player.Permission.SeniorAdmin, 1, false));
            commands.Add(new Rules("rules", "list server rules. syntax: !rules", "r", Player.Permission.User, 0, false));
            commands.Add(new PrivateMessage("privatemessage", "send message to other player. syntax: !pm <player> <message>", "pm", Player.Permission.User, 2, true));
            commands.Add(new _Stats("stats", "view your stats or another player's. syntax: !stats", "xlrstats", Player.Permission.User, 0, true));
            commands.Add(new TopStats("topstats", "view the top 4 players on this server. syntax: !topstats", "xlrtopstats", Player.Permission.User, 0, false));
            commands.Add(new Reload("reload", "reload configurations. syntax: !reload", "reload", Player.Permission.Owner, 0, false));
            commands.Add(new Balance("balance", "balance teams. syntax !balance", "bal", Player.Permission.Moderator, 0, false));
            commands.Add(new GoTo("goto", "teleport to selected player. syntax !goto", "go", Player.Permission.SeniorAdmin, 1, true));
            commands.Add(new Flag("flag", "flag a suspicious player and announce to admins on join . syntax !flag <player>:", "flag", Player.Permission.Moderator, 1, true));
            commands.Add(new _Report("report", "report a player for suspicious behaivor. syntax !report <player> <reason>", "rep", Player.Permission.User, 2, true));
            commands.Add(new Reports("reports", "get most recent reports. syntax !reports", "reports", Player.Permission.Moderator, 0, false));
            commands.Add(new _Tell("tell", "send onscreen message to player. syntax !tell <player> <message>", "t", Player.Permission.Moderator, 2, true));
            commands.Add(new Mask("mask", "hide your online presence from online admin list. syntax: !mask", "mask", Player.Permission.Administrator, 0, false));
            commands.Add(new BanInfo("baninfo", "get information about a ban for a player. syntax: !baninfo <player>", "bi", Player.Permission.Moderator, 1, true));
            commands.Add(new Alias("alias", "get past aliases and ips of a player. syntax: !alias <player>", "known", Player.Permission.Moderator, 1, true));
            commands.Add(new _RCON("rcon", "send rcon command to server. syntax: !rcon <command>", "rcon", Player.Permission.Owner, 1, false));
            commands.Add(new FindAll("findall", "find a player by their aliase(s). syntax: !findall <player>", "fa", Player.Permission.Moderator, 1, false));
        }

        //Objects
        public Log Log;
        public RCON RCON;
        public ClientsDB clientDB;
        public AliasesDB aliasDB;
        public StatsDB statDB;
        public List<Ban> Bans;
        public Player owner;
        public List<Map> maps;
        public List<String> rules;
        public Queue<Event> events;
        public Heartbeat HB;
        public String Website;
        public String Gametype;
        public int totalKills = 0;
        public List<Report> Reports;
        public List<Chat> chatHistory;
        public Queue<pHistory> playerHistory;

        //Info
        private String IP;
        private int Port;
        private String hostname;
        private String mapname;
        private int clientnum;
        private string rcon_pass;
        private List<Player> players;
        private List<Command> commands;
        private List<String> messages;
        private int messageTime;
        private TimeSpan lastMessage;
        private int nextMessage;
        private String IW_Ver;
        private int maxClients;
        private Dictionary<String, Object> Macros;
        private Moserware.TrueSkill Skills;
        private DateTime lastWebChat;
        private int Handle;
        private int PID;
        private String backupRotation;
        private int backupTimeLimit;

        //Will probably move this later
        public Dictionary<String, Player> statusPlayers;
        public bool isRunning;
        private DateTime lastPoll;
     
        //Log stuff
        private String Basepath;
        private String Mod;
        private String logPath;
        private file logFile;
    }
}
