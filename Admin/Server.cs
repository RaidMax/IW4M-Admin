using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using SharedLibrary;

using SharedLibrary.Network;
using System.Threading.Tasks;
using System.Net;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        public IW4MServer(SharedLibrary.Interfaces.IManager mgr, string address, int port, string password) : base(mgr, address, port, password) 
        {
            commandQueue = new Queue<string>();
            initCommands();
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
        override public async Task<bool> AddPlayer(Player P)
        {
            if (P.clientID < 0 || P.clientID > (Players.Count-1)) // invalid index
                return false;

            if (P.Name == null || P.Name == String.Empty || P.Name.Length <= 1)
                await this.ExecuteCommandAsync("clientkick " + P.clientID + " \"Please set a name using /name ^7\"");

           if (Players.Find(existingPlayer => (existingPlayer != null && existingPlayer.Name.Equals(P.Name)) && existingPlayer.clientID != P.clientID) != null)
                await this.ExecuteCommandAsync("clientkick " + P.clientID + " \"Someone is using your name. Set a new name using ^5/name ^7\"");

            if (Players[P.clientID] != null && Players[P.clientID].npID == P.npID) // if someone has left and a new person has taken their spot between polls
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


                List<Player> Admins = clientDB.getAdmins();
                if (Admins.Find(x => x.Name == P.Name) != null)
                {
                    if ((Admins.Find(x => x.Name == P.Name).npID != P.npID) && NewPlayer.Level < Player.Permission.Moderator)
                        await this.ExecuteCommandAsync("clientkick " + P.clientID + " \"Please do not impersonate an admin^7\"");
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

                await ExecuteEvent(new Event(Event.GType.Connect, "", NewPlayer, null, this));

                if (NewPlayer.Level == Player.Permission.Banned) // their guid is already banned so no need to check aliases
                {
                    String Message;

                    Log.Write("Banned client " + P.Name + " trying to connect...", Log.Level.Debug);

                    if (NewPlayer.lastOffense != null)
                        Message = "Previously banned for ^5" + NewPlayer.lastOffense;
                    else
                        Message = "Previous Ban";

                    await NewPlayer.Kick(Message, NewPlayer);

                    if (Players[NewPlayer.clientID] != null)
                    {
                        lock (Players)
                        {
                            Players[NewPlayer.clientID] = null;
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
                            await NewPlayer.Ban("^7Previously Banned: ^5" + B.Reason, NewPlayer);
                        else
                            await NewPlayer.Ban("^7Previous Ban", NewPlayer);

                        lock (Players)
                        {
                            if (Players[NewPlayer.clientID] != null)
                                Players[NewPlayer.clientID] = null;
                        }
                        return true;
                    }
                }

                lock (Players)
                {
                    Players[NewPlayer.clientID] = null; // just in case we have shit in the way
                    Players[NewPlayer.clientID] = NewPlayer;
                }
#if DEBUG == FALSE
                await NewPlayer.Tell($"Welcome ^5{NewPlayer.Name} ^7this is your ^5{NewPlayer.TimesConnected()} ^7time connecting!");
#endif
                if (NewPlayer.Name == "nosTEAM")
                    await NewPlayer.Tell("We encourage you to change your ^5name ^7using ^5/name^7");

                Log.Write("Client " + NewPlayer.Name + " connecting...", Log.Level.Debug); // they're clean

                while (chatHistory.Count > Math.Ceiling((double)ClientNum / 2))
                    chatHistory.RemoveAt(0);
                chatHistory.Add(new Chat(NewPlayer.Name, "<i>CONNECTED</i>", DateTime.Now));

                if (NewPlayer.Level > Player.Permission.Moderator)
                    await NewPlayer.Tell("There are ^5" + Reports.Count + " ^7recent reports!");

                ClientNum++;
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
        override public async Task RemovePlayer(int cNum)
        {
            if (cNum >= 0 && cNum < Players.Count)
            {
                Player Leaving = Players[cNum];
                Leaving.Connections++;
                clientDB.updatePlayer(Leaving);

                Log.Write("Client at " + cNum + " disconnecting...", Log.Level.Debug);
                await ExecuteEvent(new Event(Event.GType.Disconnect, "", Leaving, null, this));
                lock (Players)
                {
                    Players[cNum] = null;
                }

                ClientNum--;
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
                    P = Players[pID];
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
        // todo: this needs to be removed out of here
        override public async Task<Command> ProcessCommand(Event E, Command C)
        {
            E.Data = E.Data.RemoveWords(1);
            String[] Args = E.Data.Trim().Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            if (E.Origin.Level < C.Permission)
            {
                await E.Origin.Tell("You do not have access to that command!");
                return null;
            }

            if (Args.Length < (C.requiredArgNum))
            {
                await E.Origin.Tell("Not enough arguments supplied!");
                return null;
            }

           if (C.needsTarget)
            {
                int cNum = -1;
                int.TryParse(Args[0], out cNum);

                // this is so ugly wtf is it doing here?
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
                    if (Players[cNum] != null)
                        E.Target = Players[cNum];
                }

                else
                    E.Target = clientFromName(Args[0]);

                if (E.Target == null)
                {
                    await E.Origin.Tell("Unable to find specified player.");
                    return null;
                }
            }
            return C;
        }

        public override async Task ExecuteEvent(Event E)
        {
            await ProcessEvent(E);

            foreach (SharedLibrary.Extensions.IPlugin P in PluginImporter.potentialPlugins)
            {
                try
                {
                    await P.OnEvent(E, this);
                }

                catch (Exception Except)
                {
                    Log.Write(String.Format("The plugin \"{0}\" generated an error. ( see log )", P.Name), Log.Level.Production);
                    Log.Write(String.Format("Error Message: {0}", Except.Message), Log.Level.Debug);
                    Log.Write(String.Format("Error Trace: {0}", Except.StackTrace), Log.Level.Debug);
                    continue;
                }
            }
        }

        async Task PollPlayersAsync()
        {
            var CurrentPlayers = await this.GetStatusAsync();

            for (int i = 0; i < Players.Count; i++)
                if (CurrentPlayers.Find(p => p.clientID == i) == null && Players[i] != null)
                    await RemovePlayer(i);

            foreach (Player P in CurrentPlayers)
               await AddPlayer(P);
        }

        long l_size = -1;
        String[] lines = new String[8];
        String[] oldLines = new String[8];
        DateTime start = DateTime.Now;
        DateTime playerCountStart = DateTime.Now;
        DateTime lastCount = DateTime.Now;
        DateTime tickTime = DateTime.Now;

        override public async Task<int> ProcessUpdatesAsync()
        {
#if DEBUG == false
               try
#endif
            {
                await PollPlayersAsync();

                lastMessage = DateTime.Now - start;
                lastCount = DateTime.Now;

                if ((DateTime.Now - tickTime).TotalMilliseconds >= 1000)
                {
                    foreach (var Plugin in PluginImporter.potentialPlugins)
                       await Plugin.OnTick(this);

                    tickTime = DateTime.Now;
                }

                if ((lastCount - playerCountStart).TotalMinutes > 4)
                {
                    while (playerHistory.Count > 144) // 12 times a minute for 12 hours
                        playerHistory.Dequeue();
                    playerHistory.Enqueue(new PlayerHistory(lastCount, ClientNum));
                    playerCountStart = DateTime.Now;
                }

                if (lastMessage.TotalSeconds > messageTime && messages.Count > 0 && Players.Count > 0)
                {
                    initMacros(); // somethings dynamically change so we have to re-init the dictionary
                    await Broadcast(Utilities.LoadMacro(Macros, messages[nextMessage]));
                    if (nextMessage == (messages.Count - 1))
                        nextMessage = 0;
                    else
                        nextMessage++;
                    start = DateTime.Now;
                }
            
                //logFile = new IFile();
                if (l_size != logFile.getSize())
                {
                    // this should be the longest running task
                    await Task.FromResult(lines = logFile.Tail(12));
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

                            if (lines[count].Length < 10) // it's not a needed line 
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
                                    await ExecuteEvent(event_);
                                }
                            }

                        }
                    }
                }
                oldLines = lines;
                l_size = logFile.getSize();

                return 1;

            }
#if DEBUG == false
                catch (Exception E)
                {
                    Log.Write("Unexpected error on \"" + Hostname + "\"", Log.Level.Debug);
                    Log.Write("Error Message: " + E.Message, Log.Level.Debug);
                    Log.Write("Error Trace: " + E.StackTrace, Log.Level.Debug);
                    return 1;
            }
#endif
        }

        public async Task Initialize()
        {
            var shortversion = await this.GetDvarAsync<string>("shortversion");
            var hostname = await this.GetDvarAsync<string>("sv_hostname");
            var mapname = await this.GetDvarAsync<string>("mapname");
            var maxplayers = await this.GetDvarAsync<int>("party_maxplayers");
            var gametype = await this.GetDvarAsync<string>("g_gametype");
            var basepath = await this.GetDvarAsync<string>("fs_basepath");
            var game = await this.GetDvarAsync<string>("fs_game");
            var logfile = await this.GetDvarAsync<string>("g_log");
            var logsync = await this.GetDvarAsync<int>("g_logsync");

            try
            {
                var website = await this.GetDvarAsync<string>("_website");
                Website = website.Value;
            }

            catch (SharedLibrary.Exceptions.DvarException)
            {
                Website = "this server's website";
            }

            this.Hostname = hostname.Value.StripColors();
            this.CurrentMap = maps.Find(m => m.Name == mapname.Value) ?? new Map(mapname.Value, mapname.Value);
            this.MaxClients = maxplayers.Value;
            this.FSGame = game.Value;

            await this.SetDvarAsync("sv_kickbantime", 3600);
            await this.SetDvarAsync("sv_network_fps", 1000);
            await this.SetDvarAsync("com_maxfps", 1000);

            if (logsync.Value != 1 || logfile.Value == string.Empty)
            {
                // this DVAR isn't set until the a map is loaded
                await this.SetDvarAsync("g_logsync", 1);
                await this.SetDvarAsync("g_log", "logs/games_mp.log");
                await this.ExecuteCommandAsync("map_restart");
                logfile = await this.GetDvarAsync<string>("g_log");
            }
#if DEBUG
            basepath.Value = @"\\tsclient\K\MW2";
#endif
            string logPath = string.Empty;

            if (game.Value == "")
                logPath = $"{basepath.Value.Replace("\\", "/")}/userraw/{logfile.Value}";
            else
                logPath = $"{basepath.Value.Replace("\\", "/")}/{game.Value}/{logfile.Value}";

            if (!File.Exists(logPath))
            {
                Log.Write($"Gamelog {logPath} does not exist!", Log.Level.All);
            }

            logFile = new IFile(logPath);
            Log.Write("Log file is " + logPath, Log.Level.Debug);
            await ExecuteEvent(new Event(Event.GType.Start, "Server started", null, null, this));
            //Bans = clientDB.getBans();
#if !DEBUG
            Broadcast("IW4M Admin is now ^2ONLINE");
#endif
        }

        //Process any server event
        override protected async Task ProcessEvent(Event E)
        {
            if (E.Type == Event.GType.Connect)
            {
                return;
            }

            if (E.Type == Event.GType.Disconnect)
            {
                if (E.Origin == null)
                {
                    Log.Write("Disconnect event triggered, but no origin found.", Log.Level.Debug);
                    return;
                }

                while (chatHistory.Count > Math.Ceiling(((double)ClientNum - 1) / 2))
                    chatHistory.RemoveAt(0);
                chatHistory.Add(new Chat(E.Origin.Name, "<i>DISCONNECTED</i>", DateTime.Now));

                //removePlayer(E.Origin.clientID);        
                return;
            }

            if (E.Type == Event.GType.Kill)
            {
                if (E.Origin == null)
                {
                    Log.Write("Kill event triggered, but no origin found!", Log.Level.Debug);
                    return;
                }

                if (E.Origin != E.Target)
                {
                    await ExecuteEvent(new Event(Event.GType.Death, E.Data, E.Target, null, this));
                }

                else // suicide/falling
                {
                    //Log.Write(E.Origin.Name + " suicided...", Log.Level.Debug);
                    await ExecuteEvent(new Event(Event.GType.Death, "suicide", E.Target, null, this));
                }
            }

            if (E.Type == Event.GType.Say)
            {
                if (E.Data.Length < 2) // ITS A LIE!
                    return;

                if (E.Origin == null)
                {
                    Log.Write("Say event triggered, but no origin found! - " + E.Data, Log.Level.Debug);
                    return;
                }


                if (E.Owner == null)
                {
                    Log.Write("Say event does not have an owner!", Log.Level.Debug);
                    return;
                }

                if (E.Data.Substring(0, 1) == "!" || E.Origin.Level == Player.Permission.Console)
                {
                    Command C = E.isValidCMD(Manager.GetCommands());

                    if (C != null)
                    {
                        C = await ProcessCommand(E, C);
                        if (C != null)
                        {
                            if (C.needsTarget && E.Target == null)
                            {
                                Log.Write("Requested event requiring target does not have a target!", Log.Level.Debug);
                                return;
                            }

                            try
                            {
                                await C.ExecuteAsync(E);
                            }

                            catch (Exception Except)
                            {
                                Log.Write(String.Format("A command request \"{0}\" generated an error.", C.Name, Log.Level.Debug));
                                Log.Write(String.Format("Error Message: {0}", Except.Message), Log.Level.Debug);
                                Log.Write(String.Format("Error Trace: {0}", Except.StackTrace), Log.Level.Debug);
                                return;
                            }
                        }

                        else
                        {
                            Log.Write("Player didn't properly enter command - " + E.Origin.Name, Log.Level.Debug);
                            return;
                        }
                    }

                    else
                        await E.Origin.Tell("You entered an invalid command!");
                    return;
                }

                else // Not a command
                {
                    E.Data = E.Data.StripColors().CleanChars();
                    if (E.Data.Length > 50)
                        E.Data = E.Data.Substring(0, 50) + "...";
                    while (chatHistory.Count > Math.Ceiling((double)ClientNum / 2))
                        chatHistory.RemoveAt(0);

                    chatHistory.Add(new Chat(E.Origin.Name, E.Data, DateTime.Now));

                    return;
                }
            }

            if (E.Type == Event.GType.MapChange)
            {
                Log.Write("New map loaded - " + ClientNum + " active players", Log.Level.Debug);

                // make async
                Gametype = (await this.GetDvarAsync<string>("g_gametype")).Value.StripColors();
                Hostname = (await this.GetDvarAsync<string>("sv_hostname")).Value.StripColors();
                FSGame = (await this.GetDvarAsync<string>("fs_game")).Value.StripColors();

                string mapname = this.GetDvarAsync<string>("mapname").Result.Value;
                CurrentMap = maps.Find(m => m.Name == mapname) ?? new Map(mapname, mapname);

                return;
            }

            if (E.Type == Event.GType.MapEnd)
            {
                Log.Write("Game ending...", Log.Level.Debug);
                return;
            };
        }

        public override async Task Warn(String Reason, Player Target, Player Origin)
        {
            if (Target.Warnings >= 4)
                await Target.Kick("Too many warnings!", Origin);
            else
            {
                Penalty newPenalty = new Penalty(Penalty.Type.Warning, SharedLibrary.Utilities.StripColors(Reason), Target.npID, Origin.npID, DateTime.Now, Target.IP);
                clientDB.addBan(newPenalty);
                foreach (var S in Manager.GetServers()) // make sure bans show up on the webfront
                    S.Bans = S.clientDB.getBans();
                Target.Warnings++;
                String Message = String.Format("^1WARNING ^7[^3{0}^7]: ^3{1}^7, {2}", Target.Warnings, Target.Name, Target.lastOffense);
                await Broadcast(Message);
            }
        }

        public override async Task Kick(String Reason, Player Target, Player Origin)
        {
            if (Target.clientID > -1)
            {
                String Message = "^1Player Kicked: ^5" + Reason;
                Penalty newPenalty = new Penalty(Penalty.Type.Kick, SharedLibrary.Utilities.StripColors(Reason.Trim()), Target.npID, Origin.npID, DateTime.Now, Target.IP);
                clientDB.addBan(newPenalty);
                foreach (Server S in Manager.GetServers()) // make sure bans show up on the webfront
                    S.Bans = S.clientDB.getBans();
                await this.ExecuteCommandAsync("clientkick " + Target.clientID + " \"" + Message + "^7\"");
            }
        }

        public override async Task TempBan(String Reason, Player Target, Player Origin)
        {
            if (Target.clientID > -1)
            {
                await this.ExecuteCommandAsync($"tempbanclient {Target.clientID } \"^1Player Temporarily Banned: ^5{ Reason } (1 hour)\"");
                Penalty newPenalty = new Penalty(Penalty.Type.TempBan, SharedLibrary.Utilities.StripColors(Reason), Target.npID, Origin.npID, DateTime.Now, Target.IP);
                await Task.Run(() =>
                {
                    // todo: single database.. again
                   foreach (Server S in Manager.GetServers()) // make sure bans show up on the webfront
                        S.Bans = S.clientDB.getBans();
                   clientDB.addBan(newPenalty);
                });
            }
        }

        private String getWebsiteString()
        {
            if (Website != null)
                return String.Format(" (appeal at {0})", Website);
            return " (appeal at this server's website)";
        }

        override public async Task Ban(String Message, Player Target, Player Origin)
        {
            if (Target == null)
            {
                Log.Write("Something really bad happened, because there's no ban target!");
                return;
            }

            // banned from all servers if active
            foreach (var server in Manager.GetServers())
            {
                if (server.getPlayers().Count > 0)
                {
                    var activeClient = server.getPlayers().Find(x => x.npID == Target.npID);
                    if (activeClient != null)
                        await server.ExecuteCommandAsync("tempbanclient " + activeClient.clientID + " \"" + Message + "^7" + getWebsiteString() + "^7\"");
                }
            }

            if (Origin != null)
            {
                Target.setLevel(Player.Permission.Banned);
                Penalty newBan = new Penalty(Penalty.Type.Ban, Target.lastOffense, SharedLibrary.Utilities.StripColors(Target.npID), Origin.npID, DateTime.Now, Target.IP);

                await Task.Run(() =>
                {
                   clientDB.addBan(newBan);
                   clientDB.updatePlayer(Target);

                   foreach (Server S in Manager.GetServers()) // make sure bans show up on the webfront
                        S.Bans = S.clientDB.getBans();
                });

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

        override public async Task Unban(String GUID, Player Target)
        {
            foreach (Penalty B in Bans)
            {
                if (B.npID == Target.npID)
                {
                    // database stuff can be time consuming
                    await Task.Run(() =>
                    {
                       clientDB.removeBan(Target.npID, Target.IP);

                       Player P = clientDB.getPlayer(Target.npID, -1);
                       P.setLevel(Player.Permission.User);
                       clientDB.updatePlayer(P);

                        // todo: single database
                        foreach (Server S in Manager.GetServers()) // make sure bans show up on the webfront
                            S.Bans = S.clientDB.getBans();
                   });
                }
            }
        }

        public override bool Reload()
        {
            return false;
        }

        public override bool _Reload()
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

        override public void initMacros()
        {
            Macros = new Dictionary<String, Object>();
            Macros.Add("TOTALPLAYERS", clientDB.totalPlayers());
            Macros.Add("TOTALKILLS", totalKills);
            Macros.Add("VERSION", IW4MAdmin.Program.Version);
        }

        override public void initCommands()
        {

            foreach (Command C in PluginImporter.potentialCommands)
                Manager.GetCommands().Add(C);

        
            Manager.GetCommands().Add(new Plugins("plugins", "view all loaded plugins. syntax: !plugins", "p", Player.Permission.Administrator, 0, false));
         
        }

        public bool commandQueueEmpty()
        {
            return commandQueue.Count == 0;
        }

        //Objects
        private Queue<String> commandQueue;
    }
}
