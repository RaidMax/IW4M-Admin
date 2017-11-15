using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Network;
using SharedLibrary.Interfaces;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        public IW4MServer(IManager mgr, ServerConfiguration cfg) : base(mgr, cfg) { }

        override public async Task<bool> AddPlayer(Player P)
        {
            if (P.ClientID < 0 || P.ClientID > (Players.Count - 1) || P.Ping < 1 || P.Ping == 999) // invalid index
                return false;

            if (Players[P.ClientID] != null && Players[P.ClientID].NetworkID == P.NetworkID) // if someone has left and a new person has taken their spot between polls
            {
                // update their ping
                Players[P.ClientID].Ping = P.Ping;
                return true;
            }

            if (P.Name.Length < 3)
            {
                await this.ExecuteCommandAsync($"clientkick {P.ClientID} \"Your name must contain atleast 3 characters.\"");
                return false;
            }

            Logger.WriteDebug($"Client slot #{P.ClientID} now reserved");

            try
            {
                Player NewPlayer = Manager.GetClientDatabase().GetPlayer(P.NetworkID, P.ClientID);

                if (NewPlayer == null) // first time connecting
                {
                    Logger.WriteDebug($"Client slot #{P.ClientID} first time connecting");
                    Manager.GetClientDatabase().AddPlayer(P);
                    NewPlayer = Manager.GetClientDatabase().GetPlayer(P.NetworkID, P.ClientID);
                    Manager.GetAliasesDatabase().AddPlayerAliases(new Aliases(NewPlayer.DatabaseID, NewPlayer.Name, NewPlayer.IP));
                }

                List<Player> Admins = Manager.GetClientDatabase().GetAdmins();
                if (Admins.Find(x => x.Name == P.Name) != null)
                {
                    if ((Admins.Find(x => x.Name == P.Name).NetworkID != P.NetworkID) && NewPlayer.Level < Player.Permission.Moderator)
                        await this.ExecuteCommandAsync("clientkick " + P.ClientID + " \"Please do not impersonate an admin^7\"");
                }

                // below this needs to be optimized ~ 425ms runtime
                NewPlayer.UpdateName(P.Name.Trim());
                NewPlayer.Alias = Manager.GetAliasesDatabase().GetPlayerAliases(NewPlayer.DatabaseID);

                if (NewPlayer.Alias == null)
                {
                    Manager.GetAliasesDatabase().AddPlayerAliases(new Aliases(NewPlayer.DatabaseID, NewPlayer.Name, NewPlayer.IP));
                    NewPlayer.Alias = Manager.GetAliasesDatabase().GetPlayerAliases(NewPlayer.DatabaseID);
                }

                if (P.lastEvent == null || P.lastEvent.Owner == null)
                    NewPlayer.lastEvent = new Event(Event.GType.Say, null, NewPlayer, null, this); // this is messy but its throwing an error when they've started in too late
                else
                    NewPlayer.lastEvent = P.lastEvent;

                // lets check aliases 
                if ((NewPlayer.Alias.Names.Find(m => m.Equals(P.Name))) == null || NewPlayer.Name == null || NewPlayer.Name == String.Empty)
                {
                    NewPlayer.UpdateName(P.Name.Trim());
                    NewPlayer.Alias.Names.Add(NewPlayer.Name);
                }

                // and ips
                if (NewPlayer.Alias.IPS.Find(i => i.Equals(P.IP)) == null || P.IP == null || P.IP == String.Empty)
                    NewPlayer.Alias.IPS.Add(P.IP);

                NewPlayer.SetIP(P.IP);

                Manager.GetAliasesDatabase().UpdatePlayerAliases(NewPlayer.Alias);
                Manager.GetClientDatabase().UpdatePlayer(NewPlayer);

                await ExecuteEvent(new Event(Event.GType.Connect, "", NewPlayer, null, this));

                if (NewPlayer.Level == Player.Permission.Banned) // their guid is already banned so no need to check aliases
                {
                    Logger.WriteInfo($"Banned client {P.Name}::{P.NetworkID} trying to connect...");
                    await NewPlayer.Kick(NewPlayer.lastOffense != null ? "^7Previously banned for ^5 " + NewPlayer.lastOffense : "^7Previous Ban", NewPlayer);

                    return true;
                }

                var newPlayerAliases = Manager.GetAliasClients(NewPlayer);

                foreach (Player aP in newPlayerAliases) // lets check their aliases
                {
                    if (aP == null)
                        continue;

                    if (aP.Level == Player.Permission.Flagged)
                        NewPlayer.SetLevel(Player.Permission.Flagged);

                    Penalty B = IsBanned(aP);

                    if (B != null && B.BType == Penalty.Type.Ban)
                    {
                        Logger.WriteDebug($"Banned client {aP.Name}::{aP.NetworkID} is connecting with new alias {NewPlayer.Name}");
                        NewPlayer.lastOffense = String.Format("Evading ( {0} )", aP.Name);

                        await NewPlayer.Ban(B.Reason != null ? "^7Previously banned for ^5 " + B.Reason : "^7Previous Ban", NewPlayer);
                        Players[NewPlayer.ClientID] = null;

                        return true;
                    }

                    var activeTB = IsTempBanned(aP);
                    if (activeTB != null)
                    {
                        await this.ExecuteCommandAsync($"clientkick {NewPlayer.ClientID} \"You are temporarily banned. ({(activeTB.Expires - DateTime.Now).TimeSpanText()} left)\"");
                    }
                }

                Players[NewPlayer.ClientID] = NewPlayer;
                Logger.WriteInfo($"Client {NewPlayer.Name}::{NewPlayer.NetworkID} connecting..."); // they're clean          

                if (NewPlayer.Level > Player.Permission.Moderator)
                    await NewPlayer.Tell("There are ^5" + Reports.Count + " ^7recent reports!");

                return true;
            }

            catch (Exception E)
            {
                Manager.GetLogger().WriteError($"Unable to add player {P.Name}::{P.NetworkID}");
                Manager.GetLogger().WriteDebug(E.StackTrace);
                return false;
            }
        }

        //Remove player by CLIENT NUMBER
        override public async Task RemovePlayer(int cNum)
        {
            if (cNum >= 0)
            {
                Player Leaving = Players[cNum];
                Leaving.Connections++;
                Manager.GetClientDatabase().UpdatePlayer(Leaving);

                Logger.WriteInfo($"Client {Leaving.Name}::{Leaving.NetworkID} disconnecting...");
                await ExecuteEvent(new Event(Event.GType.Disconnect, "", Leaving, null, this));
                Players[cNum] = null;
            }
        }

        //Another version of client from line, written for the line created by a kill or death event
        override public Player ParseClientFromString(String[] L, int cIDPos)
        {
            if (L.Length < cIDPos)
            {
                Logger.WriteError("Line sent for client creation is not long enough!");
                return null;
            }

            int pID = -2; // apparently falling = -1 cID so i can't use it now
            int.TryParse(L[cIDPos].Trim(), out pID);

            if (pID == -1) // special case similar to mod_suicide
                int.TryParse(L[2], out pID);

            if (pID < 0 || pID > 17)
            {
                Logger.WriteError("Event player index " + pID + " is out of bounds!");
                Logger.WriteDebug("Offending line -- " + String.Join(";", L));
                return null;
            }

            return Players[pID];
        }

        //Check ban list for every banned player and return ban if match is found 
        override public Penalty IsBanned(Player C)
        {
            return Manager.GetClientPenalties().FindPenalties(C).Where(b => b.BType == Penalty.Type.Ban).FirstOrDefault();
        }

        public Penalty IsTempBanned(Player C)
        {
            return Manager.GetClientPenalties().FindPenalties(C).FirstOrDefault(b => b.BType == Penalty.Type.TempBan && b.Expires > DateTime.Now);
        }

        //Process requested command correlating to an event
        // todo: this needs to be removed out of here
        override public async Task<Command> ValidateCommand(Event E)
        {
            string CommandString = E.Data.Substring(1, E.Data.Length - 1).Split(' ')[0];
            E.Message = E.Data;

            Command C = null;
            foreach (Command cmd in Manager.GetCommands())
            {
                if (cmd.Name == CommandString.ToLower() || cmd.Alias == CommandString.ToLower())
                    C = cmd;
            }

            if (C == null)
            {
                await E.Origin.Tell("You entered an unknown command");
                throw new SharedLibrary.Exceptions.CommandException($"{E.Origin} entered unknown command \"{CommandString}\"");
            }

            E.Data = E.Data.RemoveWords(1);
            String[] Args = E.Data.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (E.Origin.Level < C.Permission)
            {
                await E.Origin.Tell("You do not have access to that command!");
                throw new SharedLibrary.Exceptions.CommandException($"{E.Origin} does not have access to \"{C.Name}\"");
            }

            if (Args.Length < (C.RequiredArgumentCount))
            {
                await E.Origin.Tell($"Not enough arguments supplied!");
                await E.Origin.Tell(C.Syntax);
                throw new SharedLibrary.Exceptions.CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
            }

            if (C.RequiresTarget || Args.Length > 0)
            {
                int cNum = -1;
                int.TryParse(Args[0], out cNum);

                if (Args[0] == String.Empty)
                    return C;

                if (Args[0][0] == '@') // user specifying target by database ID
                {
                    int dbID = -1;
                    int.TryParse(Args[0].Substring(1, Args[0].Length - 1), out dbID);

                    Player found = Manager.GetClientDatabase().GetPlayer(dbID);
                    if (found != null)
                    {
                        E.Target = found;
                        E.Target.lastEvent = E;
                        E.Owner = this as IW4MServer;
                    }
                }

                else if (Args[0].Length < 3 && cNum > -1 && cNum < 18) // user specifying target by client num
                {
                    if (Players[cNum] != null)
                        E.Target = Players[cNum];
                }

                if (E.Target == null) // Find active player including quotes (multiple words)
                    E.Target = GetClientByName(E.Data.Trim());

                if (E.Target == null) // Find active player as single word
                    E.Target = GetClientByName(Args[0]);

                if (E.Target == null && C.RequiresTarget)
                {
                    await E.Origin.Tell("Unable to find specified player.");
                    throw new SharedLibrary.Exceptions.CommandException($"{E.Origin} specified invalid player for \"{C.Name}\"");
                }
            }
            return C;
        }

        public override async Task ExecuteEvent(Event E)
        {
            if (Throttled)
                return;

            await ProcessEvent(E);

            foreach (IPlugin P in SharedLibrary.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    await P.OnEventAsync(E, this);
                }

                catch (Exception Except)
                {
                    Logger.WriteError(String.Format("The plugin \"{0}\" generated an error. ( see log )", P.Name));
                    Logger.WriteDebug(String.Format("Error Message: {0}", Except.Message));
                    Logger.WriteDebug(String.Format("Error Trace: {0}", Except.StackTrace));
                    continue;
                }
            }
        }

        async Task<int> PollPlayersAsync()
        {
#if DEBUG
            return Players.Where(p => p != null).Count();
#endif
            var CurrentPlayers = await this.GetStatusAsync();

            for (int i = 0; i < Players.Count; i++)
            {
                if (CurrentPlayers.Find(p => p.ClientID == i) == null && Players[i] != null)
                    await RemovePlayer(i);
            }

            foreach (var P in CurrentPlayers)
                await AddPlayer(P);

            return CurrentPlayers.Count;
        }

        long l_size = -1;
        String[] lines = new String[8];
        String[] oldLines = new String[8];
        DateTime start = DateTime.Now;
        DateTime playerCountStart = DateTime.Now;
        DateTime lastCount = DateTime.Now;
        DateTime tickTime = DateTime.Now;

        override public async Task ProcessUpdatesAsync(CancellationToken cts)
        {
#if DEBUG == false
            try
#endif
            {

                if ((DateTime.Now - LastPoll).TotalMinutes < 2 && ConnectionErrors >= 1)
                    return;

                try
                {
                    await PollPlayersAsync();

                    if (ConnectionErrors > 0)
                    {
                        Logger.WriteVerbose($"Connection has been reestablished with {IP}:{Port}");
                        Throttled = false;
                    }
                    ConnectionErrors = 0;
                    LastPoll = DateTime.Now;
                }

                catch (SharedLibrary.Exceptions.NetworkException e)
                {
                    ConnectionErrors++;
                    if (ConnectionErrors == 1)
                    {
                        Logger.WriteError($"{e.Message} {IP}:{Port}, reducing polling rate");
                        Logger.WriteDebug($"Internal Exception: {e.Data["internal_exception"]}");
                        Throttled = true;
                    }
                    return;
                }

                LastMessage = DateTime.Now - start;
                lastCount = DateTime.Now;

                if ((DateTime.Now - tickTime).TotalMilliseconds >= 1000)
                {
                    foreach (var Plugin in SharedLibrary.Plugins.PluginImporter.ActivePlugins)
                        await Plugin.OnTickAsync(this);
                    tickTime = DateTime.Now;
                }

                if ((lastCount - playerCountStart).TotalMinutes >= SharedLibrary.Helpers.PlayerHistory.UpdateInterval)
                {
                    while (PlayerHistory.Count > ((60 / SharedLibrary.Helpers.PlayerHistory.UpdateInterval) * 12)) // 12 times a hour for 12 hours
                        PlayerHistory.Dequeue();
                    PlayerHistory.Enqueue(new SharedLibrary.Helpers.PlayerHistory(ClientNum));
                    playerCountStart = DateTime.Now;
                }

                if (LastMessage.TotalSeconds > MessageTime && BroadcastMessages.Count > 0 && ClientNum > 0)
                {
                    await Broadcast(Utilities.ProcessMessageToken(Manager.GetMessageTokens(), BroadcastMessages[NextMessage]));
                    NextMessage = NextMessage == (BroadcastMessages.Count - 1) ? 0 : NextMessage + 1;
                    start = DateTime.Now;
                }

                if (LogFile == null)
                    return;

                if (l_size != LogFile.Length())
                {
                    // this should be the longest running task
                    await Task.FromResult(lines = LogFile.Tail(12));
                    if (lines != oldLines)
                    {
                        l_size = LogFile.Length();
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
                                Event event_ = Event.ParseEventString(game_event, this);
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
                l_size = LogFile.Length();
            }
#if DEBUG == false
            catch (SharedLibrary.Exceptions.NetworkException)
            {
                Logger.WriteError($"Could not communicate with {IP}:{Port}");
            }

            catch (Exception E)
            {
                Logger.WriteError($"Encountered error on {IP}:{Port}");
                Logger.WriteDebug("Error Message: " + E.Message);
                Logger.WriteDebug("Error Trace: " + E.StackTrace);
            }
#endif
        }

        public async Task Initialize()
        {
            var version = await this.GetDvarAsync<string>("version");
            GameName = Utilities.GetGame(version.Value);

            if (GameName == Game.UKN)
                Logger.WriteWarning($"Game name not recognized: {version}");

            var shortversion = await this.GetDvarAsync<string>("shortversion");
            var hostname = await this.GetDvarAsync<string>("sv_hostname");
            var mapname = await this.GetDvarAsync<string>("mapname");
            var maxplayers = (GameName == Game.IW4) ?  // gotta love IW4 idiosyncrasies
                await this.GetDvarAsync<int>("party_maxplayers") :
                await this.GetDvarAsync<int>("sv_maxclients");
            var gametype = await this.GetDvarAsync<string>("g_gametype");
            var basepath = await this.GetDvarAsync<string>("fs_basepath");
            var game = await this.GetDvarAsync<string>("fs_game");
            var logfile = await this.GetDvarAsync<string>("g_log");
            var logsync = await this.GetDvarAsync<int>("g_logsync");

            DVAR<int> onelog = null;
            if (GameName == Game.IW4)
                onelog = await this.GetDvarAsync<int>("iw4x_onelog");

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
            this.CurrentMap = Maps.Find(m => m.Name == mapname.Value) ?? new Map(mapname.Value, mapname.Value);
            this.MaxClients = maxplayers.Value;
            this.FSGame = game.Value;

            await this.SetDvarAsync("sv_kickbantime", 60);
            await this.SetDvarAsync("sv_network_fps", 1000);
            await this.SetDvarAsync("com_maxfps", 1000);

            if (logsync.Value == 0 || logfile.Value == string.Empty)
            {
                // this DVAR isn't set until the a map is loaded
                await this.SetDvarAsync("logfile", 2);
                await this.SetDvarAsync("g_logsync", 2); // set to 2 for continous in other games, clamps to 1 for IW4
                await this.SetDvarAsync("g_log", "games_mp.log");
                Logger.WriteWarning("Game log file not properly initialized, restarting map...");
                await this.ExecuteCommandAsync("map_restart");
                logfile = await this.GetDvarAsync<string>("g_log");
            }

            CustomCallback = await ScriptLoaded();
#if DEBUG
            {
                basepath.Value = (GameName == Game.IW4) ?
                    @"\\tsclient\J\WIN7_10.25\MW2" :
                    @"\\tsclient\G\Program Files (x86)\Steam\SteamApps\common\Call of Duty 4";
            }

#endif
            string mainPath = (GameName == Game.IW4) ? "userraw" : "main";

            string logPath = (game.Value == "" || onelog?.Value == 1) ?
                $"{ basepath.Value.Replace("\\", "/")}/{mainPath}/{logfile.Value}" :
                $"{basepath.Value.Replace("\\", "/")}/{game.Value}/{logfile.Value}";

            if (!File.Exists(logPath))
            {
                Logger.WriteError($"Gamelog {logPath} does not exist!");
#if !DEBUG
                throw new SharedLibrary.Exceptions.ServerException($"Invalid gamelog file {logPath}");
#endif
            }
            else
                LogFile = new IFile(logPath);

            Logger.WriteInfo("Log file is " + logPath);
            await ExecuteEvent(new Event(Event.GType.Start, "Server started", null, null, this));
#if !DEBUG
            Broadcast("IW4M Admin is now ^2ONLINE");
#endif
        }

        //Process any server event
        override protected async Task ProcessEvent(Event E)
        {
            if (E.Type == Event.GType.Connect)
            {
                ChatHistory.Add(new Chat(E.Origin.Name, "<i>CONNECTED</i>", DateTime.Now));
            }

            else if (E.Type == Event.GType.Disconnect)
            {
                ChatHistory.Add(new Chat(E.Origin.Name, "<i>DISCONNECTED</i>", DateTime.Now));
            }

            else if (E.Type == Event.GType.Script)
            {
                if (E.Origin == E.Target)// suicide/falling
                    await ExecuteEvent(new Event(Event.GType.Death, E.Data, E.Target, E.Target, this));
                else
                {
                    await ExecuteEvent(new Event(Event.GType.Kill, E.Data, E.Origin, E.Target, this));
                    await ExecuteEvent(new Event(Event.GType.Death, E.Data, E.Target, E.Origin, this));
                }
            }

            if (E.Type == Event.GType.Say && E.Data.Length >= 2)
            {
                if (E.Data.Substring(0, 1) == "!" || E.Data.Substring(0, 1) == "@" || E.Origin.Level == Player.Permission.Console)
                {
                    Command C = null;

                    try
                    {
                        C = await ValidateCommand(E);
                    }

                    catch (SharedLibrary.Exceptions.CommandException e)
                    {
                        Logger.WriteInfo(e.Message);
                    }

                    if (C != null)
                    {
                        if (C.RequiresTarget && E.Target == null)
                        {
                            Logger.WriteWarning("Requested event (command) requiring target does not have a target!");
                        }

                        try
                        {
                            await C.ExecuteAsync(E);
                        }

                        catch (Exception Except)
                        {
                            Logger.WriteError(String.Format("A command request \"{0}\" generated an error.", C.Name));
                            Logger.WriteDebug(String.Format("Error Message: {0}", Except.Message));
                            Logger.WriteDebug(String.Format("Error Trace: {0}", Except.StackTrace));
                            await E.Origin.Tell("^1An internal error occured while processing your command^7");
#if DEBUG
                            await E.Origin.Tell(Except.Message);
#endif
                        }
                    }
                }

                else // Not a command
                {
                    E.Data = E.Data.StripColors().CleanChars();
                    // this should not be done for all messages.
                    //if (E.Data.Length > 50)
                    //   E.Data = E.Data.Substring(0, 50) + "...";

                    ChatHistory.Add(new Chat(E.Origin.Name, E.Data, DateTime.Now));
                }
            }

            if (E.Type == Event.GType.MapChange)
            {
                Logger.WriteInfo($"New map loaded - {ClientNum} active players");

                Gametype = (await this.GetDvarAsync<string>("g_gametype")).Value.StripColors();
                Hostname = (await this.GetDvarAsync<string>("sv_hostname")).Value.StripColors();
                FSGame = (await this.GetDvarAsync<string>("fs_game")).Value.StripColors();

                string mapname = this.GetDvarAsync<string>("mapname").Result.Value;
                CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map(mapname, mapname);
            }

            if (E.Type == Event.GType.MapEnd)
            {
                Logger.WriteInfo("Game ending...");
            }

            //todo: move
            while (ChatHistory.Count > Math.Ceiling((double)ClientNum / 2))
                ChatHistory.RemoveAt(0);

            // the last client hasn't fully disconnected yet
            // so there will still be at least 1 client left
            if (ClientNum < 2)
                ChatHistory.Clear();
        }

        public override async Task Warn(String Reason, Player Target, Player Origin)
        {
            if (Target.Warnings >= 4)
                await Target.Kick("Too many warnings!", Origin);
            else
            {
                Penalty newPenalty = new Penalty(Penalty.Type.Warning, Reason.StripColors(), Target.NetworkID, Origin.NetworkID, DateTime.Now, Target.IP, DateTime.Now);
                Manager.GetClientPenalties().AddPenalty(newPenalty);
                Target.Warnings++;
                String Message = String.Format("^1WARNING ^7[^3{0}^7]: ^3{1}^7, {2}", Target.Warnings, Target.Name, Target.lastOffense);
                await Broadcast(Message);
            }
        }

        public override async Task Kick(String Reason, Player Target, Player Origin)
        {
            if (Target.ClientID > -1)
            {
                String Message = "^1Player Kicked: ^5" + Reason;
                Penalty newPenalty = new Penalty(Penalty.Type.Kick, Reason.StripColors().Trim(), Target.NetworkID, Origin.NetworkID, DateTime.Now, Target.IP, DateTime.Now);
                Manager.GetClientPenalties().AddPenalty(newPenalty);
                await this.ExecuteCommandAsync($"clientkick {Target.ClientID}  \"{Message}^7\"");
            }
        }

        public override async Task TempBan(String Reason, TimeSpan length, Player Target, Player Origin)
        {
            await this.ExecuteCommandAsync($"clientkick {Target.ClientID } \"^1Player Temporarily Banned: ^5{ Reason }\"");
            Penalty newPenalty = new Penalty(Penalty.Type.TempBan, Reason.StripColors(), Target.NetworkID, Origin.NetworkID, DateTime.Now, Target.IP, DateTime.Now + length);
            await Task.Run(() =>
            {
                Manager.GetClientPenalties().AddPenalty(newPenalty);
            });
        }

        override public async Task Ban(String Message, Player Target, Player Origin)
        {
            if (Target == null)
            {
                Logger.WriteError("Ban target is null");
                Logger.WriteDebug($"Message: {Message}");
                Logger.WriteDebug($"Origin: {Origin.Name}::{Origin.NetworkID}");
                return;
            }

            // banned from all servers if active
            foreach (var server in Manager.GetServers())
            {
                if (server.GetPlayersAsList().Count > 0)
                {
                    var activeClient = server.GetPlayersAsList().SingleOrDefault(x => x.NetworkID == Target.NetworkID);
                    if (activeClient != null)
                    {
                        await server.ExecuteCommandAsync($"clientkick {activeClient.ClientID}  \"{Message} ^7 ({Website}) ^7\"");
                        break;
                    }
                }
            }

            if (Origin != null)
            {
                Target.SetLevel(Player.Permission.Banned);
                Penalty newBan = new Penalty(Penalty.Type.Ban, Target.lastOffense, Target.NetworkID, Origin.NetworkID, DateTime.Now, Target.IP, DateTime.MaxValue);

                await Task.Run(() =>
                {
                    Manager.GetClientPenalties().AddPenalty(newBan);
                    Manager.GetClientDatabase().UpdatePlayer(Target);
                });

                lock (Reports) // threading seems to do something weird here
                {
                    List<Report> toRemove = new List<Report>();
                    foreach (Report R in Reports)
                    {
                        if (R.Target.NetworkID == Target.NetworkID)
                            toRemove.Add(R);
                    }

                    foreach (Report R in toRemove)
                    {
                        Reports.Remove(R);
                        Logger.WriteInfo("Removing report for banned GUID - " + R.Origin.NetworkID);
                    }
                }
            }
        }

        override public async Task Unban(Player Target)
        {
            // database stuff can be time consuming
            await Task.Run(() =>
            {
                var FoundPenalties = Manager.GetClientPenalties().FindPenalties(Target);

                FoundPenalties.Where(p => p.BType > Penalty.Type.Kick)
                    .All(p =>
                    {
                        Manager.GetClientPenalties().RemovePenalty(p);
                        return true;
                    });

                Player P = Manager.GetClientDatabase().GetPlayer(Target.NetworkID, -1);
                if (P.Level < Player.Permission.Trusted)
                {
                    P.SetLevel(Player.Permission.User);
                    Manager.GetClientDatabase().UpdatePlayer(P);
                }
            });
        }

        public override bool Reload()
        {
            try
            {
                InitializeMaps();
                InitializeAutoMessages();
                InitializeRules();
                return true;
            }
            catch (Exception E)
            {
                Logger.WriteError("Unable to reload configs! - " + E.Message);
                BroadcastMessages = new List<String>();
                Maps = new List<Map>();
                Rules = new List<String>();
                return false;
            }
        }

        override public void InitializeTokens()
        {
            Manager.GetMessageTokens().Add(new SharedLibrary.Helpers.MessageToken("TOTALPLAYERS", Manager.GetClientDatabase().TotalPlayers().ToString));
            Manager.GetMessageTokens().Add(new SharedLibrary.Helpers.MessageToken("VERSION", Program.Version.ToString));
        }
    }
}
