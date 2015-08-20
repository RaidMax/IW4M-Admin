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
            playerHistory = new Queue<pHistory>();
            commandQueue = new Queue<string>();   
        }

        public override void initAbstractObj()
        {
            throw new NotImplementedException();
        }
        override public void getAliases(List<Player> returnPlayers, Player Origin)
        {  
            if (Origin == null)
                return;

            List<Aliases> aliasAliases = new List<Aliases>();
            Aliases currentAliases = aliasDB.getPlayer(Origin.databaseID);

            if (currentAliases == null)
            {
                Log.Write("No aliases found for " + Origin.Name, Log.Level.Debug);
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

                            if (returnPlayers.Exists(p => p.databaseID == aliasPlayer.databaseID == false))
                            {
                                returnPlayers.Add(aliasPlayer);
                                getAliases(returnPlayers, aliasPlayer);
                            }
                        }
                    }
            }           
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
                    statDB.addPlayer(NewPlayer);
                }

                NewPlayer.updateName(P.Name.Trim());
    
                NewPlayer.stats = statDB.getStats(NewPlayer.databaseID);
                NewPlayer.Alias = aliasDB.getPlayer(NewPlayer.databaseID);

                if (NewPlayer.Alias == null)
                {
                    aliasDB.addPlayer(new Aliases(NewPlayer.databaseID, NewPlayer.Name, NewPlayer.IP));
                    NewPlayer.Alias = aliasDB.getPlayer(NewPlayer.databaseID);
                }
                
                // try not to crash if no stats!

                if (P.lastEvent == null || P.lastEvent.Owner == null)
                    NewPlayer.lastEvent = new Event(Event.GType.Say, null, NewPlayer, null, this); // this is messy but its throwing an error when they've started it too late
                else
                    NewPlayer.lastEvent = P.lastEvent;
           
                // lets check aliases 
                if ((NewPlayer.Alias.getNames().Find(m => m.Equals(P.Name))) == null || NewPlayer.Name == null || NewPlayer.Name == String.Empty) 
                {
                    NewPlayer.updateName(P.Name.Trim());
                    NewPlayer.Alias.addName(NewPlayer.Name);
                }
               
                // and ips
                if (NewPlayer.Alias.getIPS().Find(i => i.Equals(P.IP)) == null || P.IP == null || P.IP == String.Empty)
                {
                    NewPlayer.Alias.addIP(P.IP);
                }

                NewPlayer.updateIP(P.IP);

                aliasDB.updatePlayer(NewPlayer.Alias);
                clientDB.updatePlayer(NewPlayer);


                if (NewPlayer.Level == Player.Permission.Banned) // their guid is already banned so no need to check aliases
                {
                    String Message;

                    Log.Write("Banned client " + P.Name + " trying to connect...", Log.Level.Debug);

                    if (NewPlayer.lastOffense != null)
                        Message = "^7Player Kicked: Previously banned for ^5" + NewPlayer.lastOffense + " ^7(appeal at " + Website + ")";
                    else
                        Message = "Player Kicked: Previous Ban";

                    NewPlayer.Kick(Message);

                    if (players[NewPlayer.clientID] != null)
                    {
                        lock (players)
                        {
                            players[NewPlayer.clientID] = null;
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

                    if (aP.Level == Player.Permission.Flagged)
                        NewPlayer.setLevel(Player.Permission.Flagged);

                    Ban B = isBanned(aP);

                    if (B != null)
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

                if (NewPlayer.stats == null) // there seems to be an issue with stats with multiple servers. I think this should fix it
                { 
                    statDB.addPlayer(NewPlayer);
                    NewPlayer.stats = statDB.getStats(NewPlayer.databaseID);
                }

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
         override public Ban isBanned(Player C)
         {
            
                 if (C.Level == Player.Permission.Banned)
                    return Bans.Find(p => p.npID.Equals(C.npID));

                 foreach (Ban B in Bans)
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

        //Procses requested command correlating to an event
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
            lastDvarPointer = Utilities.executeCommand(PID, Dvar + " " + Value, lastDvarPointer);
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
                    processEvent(events.Dequeue());
                if (commandQueue.Count > 0)
                    lastCommandPointer = Utilities.executeCommand(PID, commandQueue.Dequeue(), lastCommandPointer);  
                Thread.Sleep(350);
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
                        Broadcast(SharedLibrary.Utilities.processMacro(Macros, messages[nextMessage]));
                        if (nextMessage == (messages.Count - 1))
                            nextMessage = 0;
                        else
                            nextMessage++;
                        start = DateTime.Now;
                        //if (timesFailed <= 3)
                         //   HB.Send();

                        String checkVer = new Connection("http://raidmax.org/IW4M/Admin/version.php").Read();
                        double checkVerNum;
                        double.TryParse(checkVer, out checkVerNum);
                        if (checkVerNum != Program.Version && checkVerNum != 0 && !checkedForOutdate)
                        {
                            messages.Add("^5IW4M Admin ^7is outdated. Please ^5update ^7to version " + checkVerNum);
                            checkedForOutdate = true;
                        }
                        
                    }

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
                    Thread.Sleep(350);
                }
#if DEBUG == false
                catch (Exception E)
                {
                    Log.Write("Something unexpected occured. Hopefully we can ignore it :)", Log.Level.All);
                    continue;
                }
#endif

            }
            isRunning = false;
            eventQueueThread.Join();
        }

        override public bool intializeBasics()
        {
           try
           {
               // inject our dll 
               if (!Utilities.initalizeInterface(PID))
               {
                   Log.Write("Could not load IW4MAdmin interface!", Log.Level.Debug);
                   return false;
               }

                // basic info dvars
               hostname = SharedLibrary.Utilities.stripColors(getDvar("sv_hostname").current);
                mapname          = getDvar("mapname").current;
                IW_Ver           = getDvar("shortversion").current;
                maxClients       = -1;
                Int32.TryParse(getDvar("party_maxplayers").current, out maxClients);

                if (maxClients == -1)
                {
                    Log.Write("Could not get max clients value", Log.Level.Debug);
                    return false;
                }

                Gametype         = getDvar("g_gametype").current;

                // important log variables
                Basepath         = getDvar("fs_basepath").current;
                Mod              = getDvar("fs_game").current;
                logPath          = getDvar("g_log").current;
                int oneLog      = -1;
                Int32.TryParse(getDvar("iw4m_onelog").current, out oneLog);

               if (oneLog == -1)
               {
                   Log.Write("Could not get iw4m_onelog value", Log.Level.Debug);
                   return false;
               }

                // our settings
               setDvar("sv_kickBanTime", "3600");      // 1 hour
               setDvar("g_logSync", "1");              // yas

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
               return true;
            }
            catch (Exception E)
            {
                Log.Write("Error during initialization - " + E.Message +"--" + E.StackTrace, Log.Level.All);
                return false;
            }
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

                    //Skills.updateNewSkill(E.Origin, E.Target);
                    statDB.updatePlayer(E.Origin);
                    statDB.updatePlayer(E.Target);

                    totalKills++;
                    Log.Write(E.Origin.Name + " killed " + E.Target.Name + " with a " + E.Data, Log.Level.Debug);
                }

                else // suicide/falling
                {
                    E.Origin.stats.Deaths++;
                    E.Origin.stats.updateKDR();
                    statDB.updatePlayer(E.Origin);
                    Log.Write(E.Origin.Name + " suicided...", Log.Level.Debug);
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

                if (E.Data.Substring(0, 1) != "!") // Not a command so who gives an F?
                {
                    E.Data = SharedLibrary.Utilities.stripColors(SharedLibrary.Utilities.cleanChars(E.Data));
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
                        if (C.needsTarget && E.Target == null)
                        {
                            Log.Write("Requested event requiring target does not have a target!", Log.Level.Debug);
                            return false;
                        }
                        C.Execute(E);
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
                    Log.Write("Updated stats for client " + P.databaseID, Log.Level.Debug);
                }
                return true;
            }

            return false;
        }

        override public void Ban(String Message, Player Target, Player Origin)
        {
            if (Target.clientID > -1)
                executeCommand("tempbanclient " + Target.clientID + " \"" + Message + "^7\"");

            if (Origin != null)
            {
                Target.setLevel(Player.Permission.Banned);
                Ban newBan = new Ban(Target.lastOffense, Target.npID, Origin.npID, DateTime.Now, Target.IP);

                clientDB.addBan(newBan);
                clientDB.updatePlayer(Target);

               // foreach (SharedLibrary.Server S in Program.getServers()) // make sure bans show up on the webfront
               //     S.Bans = S.clientDB.getBans();

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
            foreach (Ban B in Bans)
            {
                if (B.npID == Target.npID)
                {
                    clientDB.removeBan(Target.npID, Target.IP);

                    Player P = clientDB.getPlayer(Target.npID, -1);
                    P.setLevel(Player.Permission.User);
                    clientDB.updatePlayer(P);

                    return true;
                }
            }
            return false;
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

            foreach (Command C in PluginImporter.potentialPlugins)
                commands.Add(C);
        
        }

        //Objects
        public Queue<pHistory> playerHistory;
        private Queue<String> commandQueue;

        //Info

        //Will probably move this later
        private IntPtr dllPointer;
        public IntPtr lastCommandPointer;
        public IntPtr lastDvarPointer;
    }
}
