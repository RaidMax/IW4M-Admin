using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using SharedLibrary;


namespace IW4MAdmin
{
    class IW4MServer : SharedLibrary.Server
    {
        public IW4MServer(string address, int port, string password, int H, int PID) : base(address, port, password, H, PID) 
        {
            commandQueue = new Queue<string>();
        }
   
        private void getAliases(List<Aliases> returnAliases, Aliases currentAlias)
        {
            foreach(String IP in currentAlias.IPS)
            {
                List<Aliases> Matching = aliasDB.getPlayer(IP);
                foreach(Aliases I in Matching)
                {
                    if (!returnAliases.Contains(I) && returnAliases.Find(x => x.Number == I.Number) == null)
                    {
                        returnAliases.Add(I);
                        getAliases(returnAliases, I);
                    }
                }
            }
        }

        public override List<Aliases> getAliases(Player Origin)
        {
            List<Aliases> allAliases = new List<Aliases>();
            
            if (Origin == null)
                return allAliases;

            Aliases currentIdentityAliases = aliasDB.getPlayer(Origin.databaseID);

            if (currentIdentityAliases == null)
                return allAliases;

            getAliases(allAliases, currentIdentityAliases);
            return allAliases;        
        }

        //Add player object p to `players` list
        override public bool addPlayer(Player P)
        {
            if (P.clientID < 0 || P.clientID > (players.Count-1)) // invalid index
                return false;

            if (players[P.clientID] != null && players[P.clientID].npID == P.npID) // if someone has left and a new person has taken their spot between polls
                return true;

            Log.Write("Client slot #" + P.clientID + " now reserved", Log.Level.Debug);

                
#if DEBUG == false
            try
#endif
            {
                Player NewPlayer = clientDB.getPlayer(P.npID, P.clientID);

                if (NewPlayer == null) // first time connecting
                {
                    Log.Write("Client slot #" + P.clientID + " first time connecting", Log.Level.All);
                    clientDB.addPlayer(P);
                    NewPlayer = clientDB.getPlayer(P.npID, P.clientID);
                    aliasDB.addPlayer(new Aliases(NewPlayer.databaseID, NewPlayer.Name, NewPlayer.IP));
                }

                NewPlayer.updateName(P.Name.Trim());
                NewPlayer.Alias = aliasDB.getPlayer(NewPlayer.databaseID);

                if (NewPlayer.Alias == null)
                {
                    aliasDB.addPlayer(new Aliases(NewPlayer.databaseID, NewPlayer.Name, NewPlayer.IP));
                    NewPlayer.Alias = aliasDB.getPlayer(NewPlayer.databaseID);
                }
                
                if (P.lastEvent == null || P.lastEvent.Owner == null)
                    NewPlayer.lastEvent = new Event(Event.GType.Say, null, NewPlayer, null, this); // this is messy but its throwing an error when they've started it too late
                else
                    NewPlayer.lastEvent = P.lastEvent;
           
                // lets check aliases 
                if ((NewPlayer.Alias.Names.Find(m => m.Equals(P.Name))) == null || NewPlayer.Name == null || NewPlayer.Name == String.Empty) 
                {
                    NewPlayer.updateName(P.Name.Trim());
                    NewPlayer.Alias.Names.Add(NewPlayer.Name);
                }
               
                // and ips
                if (NewPlayer.Alias.IPS.Find(i => i.Equals(P.IP)) == null || P.IP == null || P.IP == String.Empty)
                {
                    NewPlayer.Alias.IPS.Add(P.IP);
                }

                NewPlayer.updateIP(P.IP);

                aliasDB.updatePlayer(NewPlayer.Alias);
                clientDB.updatePlayer(NewPlayer);

                events.Enqueue(new Event(Event.GType.Connect, "", NewPlayer, null, this));


                if (NewPlayer.Level == Player.Permission.Banned) // their guid is already banned so no need to check aliases
                {
                    String Message;

                    Log.Write("Banned client " + P.Name + " trying to connect...", Log.Level.Debug);

                    if (NewPlayer.lastOffense != null)
                        Message = "^7Player Kicked: Previously banned for ^5" + NewPlayer.lastOffense + " ^7(appeal at " + Website + ")";
                    else
                        Message = "Player Kicked: Previous Ban";

                    NewPlayer.Kick(Message, NewPlayer);

                    if (players[NewPlayer.clientID] != null)
                    {
                        lock (players)
                        {
                            players[NewPlayer.clientID] = null;
                        }
                    }

                    return true;
                }

                List<Player> newPlayerAliases = getPlayerAliases(NewPlayer);

                foreach (Player aP in newPlayerAliases) // lets check their aliases
                {
                    if (aP == null)
                        continue;

                    if (aP.Level == Player.Permission.Flagged)
                        NewPlayer.setLevel(Player.Permission.Flagged);

                    Penalty B = isBanned(aP);

                    if (B != null && B.BType == Penalty.Type.Ban)
                    {
                        Log.Write(String.Format("Banned client {0} is connecting with new alias {1}", aP.Name, NewPlayer.Name), Log.Level.Debug);
                        NewPlayer.lastOffense = String.Format("Evading ( {0} )", aP.Name);

                        if (B.Reason != null)
                            NewPlayer.Ban("^7Previously Banned: ^5" + B.Reason + " ^7(appeal at " + Website  + ")", NewPlayer);
                        else
                            NewPlayer.Ban("^7Previous Ban", NewPlayer);

                        lock (players)
                        {
                            if (players[NewPlayer.clientID] != null)
                                players[NewPlayer.clientID] = null;
                        }
                        return true;
                    }
                }

                //finally lets check their clean status :>
                //checkClientStatus(NewPlayer);

                lock (players)
                {
                    players[NewPlayer.clientID] = null; // just in case we have shit in the way
                    players[NewPlayer.clientID] = NewPlayer;
                }
#if DEBUG == FALSE
                NewPlayer.Tell("Welcome ^5" + NewPlayer.Name + " ^7this is your ^5" + SharedLibrary.Utilities.timesConnected(NewPlayer.Connections) + " ^7time connecting!");
#endif
                Log.Write("Client " + NewPlayer.Name + " connecting...", Log.Level.Debug); // they're clean

                while (chatHistory.Count > Math.Ceiling((double)clientnum / 2))
                    chatHistory.RemoveAt(0);
                chatHistory.Add(new Chat(NewPlayer, "<i>CONNECTED</i>", DateTime.Now));

                if (NewPlayer.Level == Player.Permission.Flagged)
                    ToAdmins("^1NOTICE: ^7Flagged player ^5" + NewPlayer.Name + "^7 has joined!");

                if (NewPlayer.Level > Player.Permission.Moderator)
                    NewPlayer.Tell("There are ^5" + Reports.Count + " ^7recent reports!");

                return true;
            }
#if DEBUG == false
            catch (Exception E)
            {
                Log.Write("Unable to add player " + P.Name + " - " + E.Message, Log.Level.Debug);
                return false;
            }
#endif
        }

        //Remove player by CLIENT NUMBER
        override public bool removePlayer(int cNum)
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

                Log.Write("Client at " + cNum + " disconnecting...", Log.Level.Debug);
                events.Enqueue(new Event(Event.GType.Disconnect, "", Leaving, null, this));
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
        override public Player clientFromEventLine(String[] L, int cIDPos)
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

        //Check ban list for every banned player and return ban if match is found 
         override public Penalty isBanned(Player C)
         {
            
                 if (C.Level == Player.Permission.Banned)
                    return Bans.Find(p => p.npID.Equals(C.npID));

                 foreach (Penalty B in Bans)
                 {
                     if (B.npID.Length < 5 || B.IP.Length < 5)
                         continue;

                     if (B.npID == null || C.npID == null)
                         continue;

                     if (B.npID == C.npID)
                         return B;

                     if (B.IP == null || C.IP == null)
                         continue;

                     if (C.IP == B.IP)
                         return B;
                 }

            return null;
         }

        //Process requested command correlating to an event
        override public Command processCommand(Event E, Command C)
        {
            E.Data = SharedLibrary.Utilities.removeWords(E.Data, 1);
            String[] Args = E.Data.Trim().Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            if (E.Origin.Level < C.Permission)
            {
                E.Origin.Tell("You do not have access to that command!");
                return null;
            }

            if (Args.Length < (C.requiredArgNum))
            {
                E.Origin.Tell("Not enough arguments supplied!");
                return null;
            }

            if (C.needsTarget)
            {
                int cNum = -1;
                int.TryParse(Args[0], out cNum);

                if (C.Name == "stats" && Args.Length == 1)
                    E.Target = E.Origin;

                if (Args[0] == String.Empty)
                    return C;


                if (Args[0][0] == '@') // user specifying target by database ID
                {
                    int dbID = -1;
                    int.TryParse(Args[0].Substring(1, Args[0].Length-1), out dbID);

                    IW4MServer castServer = (IW4MServer)(E.Owner);
                    Player found = castServer.clientDB.getPlayer(dbID);
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

        override public void executeCommand(String CMD)
        {
            commandQueue.Enqueue(CMD);   
        }

        override public dvar getDvar(String DvarName)
        {
            return Utilities.getDvar(PID, DvarName, lastCommandPointer);
        }

        override public void setDvar(String Dvar, String Value)
        {
            lastDvarPointer = Utilities.executeCommand(PID, Dvar + " " + "\"" + Value + "\"", lastDvarPointer);
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        private void manageEventQueue()
        {
            while(isRunning)
            {
                if (events.Count > 0)
                {
                    Event curEvent = events.Peek();

                    if (curEvent == null)
                    {
                        events.Dequeue();
                        continue;
                    }

                    processEvent(curEvent);
                    foreach (Plugin P in PluginImporter.potentialPlugins)
                    {
                        try
                        {
                            P.onEvent(curEvent);
                        }

                        catch (Exception Except)
                        {
                            Log.Write(String.Format("The plugin \"{0}\" generated an error. ( see log )", P.Name), Log.Level.Production);
                            Log.Write(String.Format("Error Message: {0}", Except.Message), Log.Level.Debug);
                            Log.Write(String.Format("Error Trace: {0}", Except.StackTrace), Log.Level.Debug);
                            continue;
                        }   
                    }
                    events.Dequeue();
                }
                if (commandQueue.Count > 0)
                    lastCommandPointer = Utilities.executeCommand(PID, commandQueue.Dequeue(), lastCommandPointer);  
                Thread.Sleep(300);
            }
        }

        //Starts the monitoring process
        override public void Monitor()
        {
            isRunning = true;

            if (!intializeBasics())
            {
                Log.Write("Stopping " + Port + " due to uncorrectable errors (check log)", Log.Level.Production);
                isRunning = false;
                return;
            }

            Thread eventQueueThread = new Thread(new ThreadStart(manageEventQueue));
            eventQueueThread.Name = "Event Queue Manager";
            eventQueueThread.Start();

            long l_size = -1;
            String[] lines = new String[8];
            String[] oldLines = new String[8];
            DateTime start = DateTime.Now;
            DateTime playerCountStart = DateTime.Now;
            DateTime lastCount = DateTime.Now;

#if DEBUG == false
            Broadcast("IW4M Admin is now ^2ONLINE");
            int numExceptions = 0;
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
                        playerHistory.Enqueue(new PlayerHistory(lastCount, clientnum));
                        playerCountStart = DateTime.Now;
                    }

                    if(lastMessage.TotalSeconds > messageTime && messages.Count > 0)
                    {
                        initMacros(); // somethings dynamically change so we have to re-init the dictionary
                        Broadcast(SharedLibrary.Utilities.processMacro(Macros, messages[nextMessage]));
                        if (nextMessage == (messages.Count - 1))
                            nextMessage = 0;
                        else
                            nextMessage++;
                        start = DateTime.Now;
                    }

                    if ((DateTime.Now - lastPoll).Milliseconds > 300)
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

                            else if (eachClient.state == 5 )
                            {
                                addPlayer(new Player(SharedLibrary.Utilities.stripColors(SharedLibrary.Utilities.cleanChars(eachClient.name)), eachClient.steamid.ToString("x16"), i, 0, i, null, 0, Helpers.NET_AdrToString(eachClient.adr).Split(':')[0]));
                                activeClients++;
                            }
                        }

                        lastPoll = DateTime.Now;
                        clientnum = activeClients;
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

                                if (lines[count].Length < 10) // its not a needed line 
                                    continue;

                                else
                                {
                                    string[] game_event = lines[count].Split(';');
                                    Event event_ = Event.requestEvent(game_event, (Server)this);
                                    if (event_ != null)
                                    {
                                        if (event_.Origin == null)
                                            continue;

                                        event_.Origin.lastEvent = event_;
                                        event_.Origin.lastEvent.Owner = this;
                                        events.Enqueue(event_);
                                    }
                                }

                            }
                        }
                    }
                    oldLines = lines;
                    l_size = logFile.getSize();
                    Thread.Sleep(300);
                }
#if DEBUG == false
                catch (Exception E)
                {
                    numExceptions++;
                    Log.Write("Unexpected error on \"" + hostname + "\"", Log.Level.Debug);
                    Log.Write("Error Message: " + E.Message, Log.Level.Debug);
                    Log.Write("Error Trace: " + E.StackTrace, Log.Level.Debug);
                    if (numExceptions < 30)
                        continue;
                    else
                    {
                        Log.Write("Maximum number of unhandled exceptions reached for \"" + hostname + "\"", Log.Level.Production);
                        events.Enqueue(new Event(Event.GType.Stop, "Monitoring stopping because of max exceptions reached", null, null, this));
                        isRunning = false;
                    }
                }
#endif

            }
            events.Enqueue(new Event(Event.GType.Stop, "Server monitoring stopped", null, null, this));
            isRunning = false;
            eventQueueThread.Join();
        }

        override public bool intializeBasics()
        {
#if DEBUG == false
            try
#endif
            {
                // clear out any lingering instances
                Utilities.shutdownInterface(PID);

                // inject our dll 
                if (!Utilities.initalizeInterface(PID))
                {
                    Log.Write("Could not load IW4MAdmin interface!", Log.Level.Debug);
                    return false;
                }

                // basic info dvars
                do
                {
                    hostname = SharedLibrary.Utilities.stripColors(getDvar("sv_hostname").current);
                    mapname = getDvar("mapname").current;
                } while (hostname == "0" || mapname == "0");

                Map localizedMapName = maps.Find(x => x.Name.Equals(mapname));

                if (localizedMapName != null)
                    mapname = localizedMapName.Alias;


                IW_Ver = getDvar("shortversion").current;
                maxClients = -1;
                Int32.TryParse(getDvar("party_maxplayers").current, out maxClients);

                if (maxClients == -1)
                {
                    Log.Write("Could not get max clients value", Log.Level.Debug);
                    return false;
                }

                Gametype = getDvar("g_gametype").current;

                // important log variables
                do
                {
                    Basepath = getDvar("fs_basepath").current;
                    Mod = getDvar("fs_game").current;
                    logPath = getDvar("g_log").current;
                } while (Basepath == "0" || Mod == "0" || logPath == "0");
                int oneLog = -1;
                logPath = logPath.Replace("/", "\\");
                Mod = Mod.Replace("/", "\\");

                Thread.Sleep(100);
               Int32.TryParse(getDvar("iw4m_onelog").current, out oneLog);

                if (oneLog == -1)
                {
                    Log.Write("Could not get iw4m_onelog value", Log.Level.Debug);
                    return false;
                }

                // our settings
                setDvar("sv_kickbantime", "3600");      // 1 hour
                Website = getDvar("_website").current;

                if (Website == "0" || Website == null)
                    Website = "this server's website";

                int logSync = -1;

                Thread.Sleep(100);
                Int32.TryParse(getDvar("g_logSync").current, out logSync);

                if (logSync == 0)
                {
                    Log.Write("g_logsync is not set to 1, restarting map...");
                    setDvar("g_logSync", "1");              // yas
                    executeCommand("map_restart");
                    SharedLibrary.Utilities.Wait(10);
                }


                if (Mod == String.Empty || oneLog == 1)
                    logPath = Basepath + '\\' + "m2demo" + '\\' + logPath;
                else
                    logPath = Basepath + '\\' + Mod + '\\' + logPath;

                if (!File.Exists(logPath))
                {
                    Log.Write("Gamelog `" + logPath + "` does not exist!", Log.Level.All);
                    return false;
                }

                logFile = new IFile(logPath);
                Log.Write("Log file is " + logPath, Log.Level.Debug);
                Log.Write("Now monitoring " + getName(), Log.Level.Production);
                events.Enqueue(new Event(Event.GType.Start, "Server started", null, null, this));
                Bans = clientDB.getBans();
                return true;
            }
#if DEBUG == false
            catch (Exception E)
            {
                Log.Write("Error during initialization - " + E.Message + "--" + E.StackTrace, Log.Level.All);
                return false;
            }
#endif
        }

        //Process any server event
        override public bool processEvent(Event E)
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

                removePlayer(E.Origin.clientID);        
                return true;
            }

            if (E.Type == Event.GType.Kill)
            {
                if (E.Origin == null)
                {
                    Log.Write("Kill event triggered, but no origin found!", Log.Level.Debug);
                    return false;
                }

                if (E.Origin != E.Target)
                {
                    events.Enqueue(new Event(Event.GType.Death, E.Data, E.Target, null, this));
                }

                else // suicide/falling
                {
                    Log.Write(E.Origin.Name + " suicided...", Log.Level.Debug);
                    events.Enqueue(new Event(Event.GType.Death, "suicide", E.Target, null, this));
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

                Log.Write("Message from " + E.Origin.Name + ": " + E.Data, Log.Level.Debug);

                if (E.Owner == null)
                {
                    Log.Write("Say event does not have an owner!", Log.Level.Debug);
                    return false;
                }

                if (E.Data.Substring(0, 1) == "!" || E.Origin.Level == Player.Permission.Console)
                {
                    Command C = E.isValidCMD(commands);

                    if (C != null)
                    {
                        C = processCommand(E, C);
                        if (C != null)
                        {
                            if (C.needsTarget && E.Target == null)
                            {
                                Log.Write("Requested event requiring target does not have a target!", Log.Level.Debug);
                                return false;
                            }

                            try
                            {
                                C.Execute(E);
                            }

                            catch (Exception Except)
                            {
                                Log.Write(String.Format("A command request \"{0}\" generated an error.", C.Name, Log.Level.Debug));
                                Log.Write(String.Format("Error Message: {0}", Except.Message), Log.Level.Debug);
                                Log.Write(String.Format("Error Trace: {0}", Except.StackTrace), Log.Level.Debug);
                                return false;
                            }
                            return true;
                        }

                        else
                        {
                            Log.Write("Player didn't properly enter command - " + E.Origin.Name, Log.Level.Debug);
                            return true;
                        }
                    }

                    else
                        E.Origin.Tell("You entered an invalid command!");
                }

                else // Not a command so who gives an F?
                {
                    E.Data = SharedLibrary.Utilities.stripColors(SharedLibrary.Utilities.cleanChars(E.Data));
                    if (E.Data.Length > 50)
                        E.Data = E.Data.Substring(0, 50) + "...";
                    while (chatHistory.Count > Math.Ceiling((double)clientnum / 2))
                        chatHistory.RemoveAt(0);

                    chatHistory.Add(new Chat(E.Origin, E.Data, DateTime.Now));

                    return true;
                }

                return true;
            }

            if (E.Type == Event.GType.MapChange)
            {
                Log.Write("New map loaded - " + clientnum + " active players", Log.Level.Debug);

                String newMapName = "0";
                String newGametype = "0";
                String newHostName = "0";

                while(newMapName == "0" || newGametype == "0" || newHostName == "0") // weird anomaly here.
                {
                   newMapName = getDvar("mapname").current;
                   newGametype = getDvar("g_gametype").current;
                   newHostName = getDvar("sv_hostname").current;
                }
                
                Map newMap = maps.Find(m => m.Name.Equals(newMapName));

                if (newMap != null)
                    mapname = newMap.Alias;
                else
                    mapname = newMapName;

                Gametype = newGametype;
                hostname = SharedLibrary.Utilities.stripColors(newHostName);

                return true;
            }

            if (E.Type == Event.GType.MapEnd)
            {
                Log.Write("Game ending...", Log.Level.Debug);
                return true;
            }

            return false;
        }

        public override void Warn(string Reason, Player Target, Player Origin)
        {
            Penalty newPenalty = new Penalty(Penalty.Type.Warning, SharedLibrary.Utilities.stripColors(Reason), Target.npID, Origin.npID, DateTime.Now, Target.IP);
            clientDB.addBan(newPenalty);
            foreach (SharedLibrary.Server S in Program.getServers()) // make sure bans show up on the webfront
                S.Bans = S.clientDB.getBans();
            Target.Warnings++;
            String Message = String.Format("^1WARNING ^7[^3{0}^7]: ^3{1}^7, {2}", Target.Warnings, Target.Name, Target.lastOffense);
            Broadcast(Message);
            if (Target.Warnings >= 4)
                Target.Kick("You were kicked for too many warnings!", Origin);
        }

        public override void Kick(string Reason, Player Target, Player Origin)
        {
            if (Target.clientID > -1)
            {
                String Message = "^1Player Kicked: ^5" + Reason + "                    ^1Admin: ^5" + Origin.Name;
                Penalty newPenalty = new Penalty(Penalty.Type.Kick, SharedLibrary.Utilities.stripColors(Reason.Split(':')[1]), Target.npID, Origin.npID, DateTime.Now, Target.IP);
                clientDB.addBan(newPenalty);
                foreach (SharedLibrary.Server S in Program.getServers()) // make sure bans show up on the webfront
                    S.Bans = S.clientDB.getBans();
                executeCommand("clientkick " + Target.clientID + " \"" + Message + "^7\"");
            }
        }

        public override void tempBan(string Reason, Player Target, Player Origin)
        {
            if (Target.clientID > -1)
            {
                executeCommand("tempbanclient " + Target.clientID + " \"" + Reason + "\"");
                Penalty newPenalty = new Penalty(Penalty.Type.TempBan, SharedLibrary.Utilities.stripColors(Reason.Split(':')[1]), Target.npID, Origin.npID, DateTime.Now, Target.IP);
                foreach (SharedLibrary.Server S in Program.getServers()) // make sure bans show up on the webfront
                    S.Bans = S.clientDB.getBans();
                clientDB.addBan(newPenalty);
            }
        }
        override public void Ban(String Message, Player Target, Player Origin)
        {
            if (Target.clientID > -1)
                executeCommand("tempbanclient " + Target.clientID + " \"" + Message + "^7\"");

            if (Origin != null)
            {
                Target.setLevel(Player.Permission.Banned);
                Penalty newBan = new Penalty(Penalty.Type.Ban, Target.lastOffense, Target.npID, Origin.npID, DateTime.Now, Target.IP);

                clientDB.addBan(newBan);
                clientDB.updatePlayer(Target);

                foreach (SharedLibrary.Server S in Program.getServers()) // make sure bans show up on the webfront
                    S.Bans = S.clientDB.getBans();

                lock (Reports) // threading seems to do something weird here
                {
                    List<Report> toRemove = new List<Report>();
                    foreach (Report R in Reports)
                    {
                        if (R.Target.npID == Target.npID)
                            toRemove.Add(R);
                    }

                    foreach (Report R in toRemove)
                    {
                        Reports.Remove(R);
                        Log.Write("Removing report for banned GUID -- " + R.Origin.npID, Log.Level.Debug);
                    }
                }
            }
        }

        override public bool Unban(String GUID, Player Target)
        {
            foreach (Penalty B in Bans)
            {
                if (B.npID == Target.npID)
                {
                    clientDB.removeBan(Target.npID, Target.IP);

                    Player P = clientDB.getPlayer(Target.npID, -1);
                    P.setLevel(Player.Permission.User);
                    clientDB.updatePlayer(P);

                    foreach (SharedLibrary.Server S in Program.getServers()) // make sure bans show up on the webfront
                        S.Bans = S.clientDB.getBans();

                    return true;
                }
            }
            return false;
        }

        override public bool Reload()
        {
            try
            {
                messages = null;
                maps = null;
                rules = null;
                initMaps();
                initMessages();
                initRules();
                PluginImporter.Unload();
                PluginImporter.Load();
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

        override public void initMacros()
        {
            Macros = new Dictionary<String, Object>();
            Macros.Add("TOTALPLAYERS", clientDB.totalPlayers());
            Macros.Add("TOTALKILLS", totalKills);
            Macros.Add("VERSION", IW4MAdmin.Program.Version);
        }

        override public void initCommands()
        {
            // Something like *COMMAND* | NAME | HELP MSG | ALIAS | NEEDED PERMISSION | # OF REQUIRED ARGS | HAS TARGET |

            commands = new List<Command>();
            owner = clientDB.getOwner();

            if(owner == null)
                commands.Add(new Owner("owner", "claim ownership of the server", "owner", Player.Permission.User, 0, false));

            foreach (Command C in PluginImporter.potentialCommands)
                commands.Add(C);

            commands.Add(new Kick("kick", "kick a player by name. syntax: !kick <player> <reason>.", "k", Player.Permission.Trusted, 2, true));
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
            commands.Add(new Warn("warn", "warn player for infringing rules syntax: !warn <player> <reason>.", "w", Player.Permission.Trusted, 2, true));
            commands.Add(new WarnClear("warnclear", "remove all warning for a player syntax: !warnclear <player>.", "wc", Player.Permission.Trusted, 1, true));
            commands.Add(new Unban("unban", "unban player by database id. syntax: !unban @<id>.", "ub", Player.Permission.SeniorAdmin, 1, true));
            commands.Add(new Admins("admins", "list currently connected admins. syntax: !admins.", "a", Player.Permission.User, 0, false));
            commands.Add(new MapCMD("map", "change to specified map. syntax: !map", "m", Player.Permission.Administrator, 1, false));
            commands.Add(new Find("find", "find player in database. syntax: !find <player>", "f", Player.Permission.SeniorAdmin, 1, false));
            commands.Add(new Rules("rules", "list server rules. syntax: !rules", "r", Player.Permission.User, 0, false));
            commands.Add(new PrivateMessage("privatemessage", "send message to other player. syntax: !pm <player> <message>", "pm", Player.Permission.User, 2, true));
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
            commands.Add(new Plugins("plugins", "view all loaded plugins. syntax: !plugins", "p", Player.Permission.Administrator, 0, false));
        }

        //Objects
        private Queue<String> commandQueue;

        //Will probably move this later
        private IntPtr dllPointer;
        public IntPtr lastCommandPointer;
        public IntPtr lastDvarPointer;
    }
}
