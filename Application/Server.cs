using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Configuration;

using IW4MAdmin.Application.Misc;
using Application.RconParsers;
using Application.EventParsers;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        private CancellationToken cts;

        public IW4MServer(IManager mgr, ServerConfiguration cfg) : base(mgr, cfg) { }

        public override int GetHashCode()
        {
            // todo: make this better with collisions
            int id = Math.Abs($"{IP}:{Port.ToString()}".Select(a => (int)a).Sum());

            // this is a nasty fix for get hashcode being changed
            switch(id)
            {
                case 765:
                    return 886229536;
                case 760:
                    return 1645744423;
                case 761:
                    return 1645809959;
            }

            return id;
        }

        override public async Task<bool> AddPlayer(Player polledPlayer)
        {
            if (polledPlayer.Ping == 999 || polledPlayer.Ping < 1 || polledPlayer.ClientNumber > (MaxClients) || polledPlayer.ClientNumber < 0)
            {
                //Logger.WriteDebug($"Skipping client not in connected state {P}");
                return true;
            }

            if (Players[polledPlayer.ClientNumber] != null &&
                Players[polledPlayer.ClientNumber].NetworkId == polledPlayer.NetworkId)
            {
                // update their ping & score 
                Players[polledPlayer.ClientNumber].Ping = polledPlayer.Ping;
                Players[polledPlayer.ClientNumber].Score = polledPlayer.Score;
                return true;
            }
#if !DEBUG
            if (polledPlayer.Name.Length < 3)
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their name is too short");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, "Your name must contain atleast 3 characters.");
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

            if (Players.FirstOrDefault(p => p != null && p.Name == polledPlayer.Name) != null)
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their name is already in use");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, "Your name is being used by someone else.");
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

            if (polledPlayer.Name == "Unknown Soldier" ||
                polledPlayer.Name == "UnknownSoldier" ||
                polledPlayer.Name == "CHEATER")
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their name is generic");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, "Please change your name using /name.");
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

            if (polledPlayer.Name.Where(c => Char.IsControl(c)).Count() > 0)
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their contains control characters");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, "Your name cannot contain control characters.");
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

#endif
            Logger.WriteDebug($"Client slot #{polledPlayer.ClientNumber} now reserved");

            try
            {
                Player player = null;
                var client = await Manager.GetClientService().GetUnique(polledPlayer.NetworkId);

                // first time client is connecting to server
                if (client == null)
                {
                    Logger.WriteDebug($"Client {polledPlayer} first time connecting");
                    player = (await Manager.GetClientService().Create(polledPlayer)).AsPlayer();
                }

                // client has connected in the past
                else
                {
                    client.LastConnection = DateTime.UtcNow;
                    client.Connections += 1;

                    var existingAlias = client.AliasLink.Children
                        .FirstOrDefault(a => a.Name == polledPlayer.Name && a.IPAddress == polledPlayer.IPAddress);

                    if (existingAlias == null)
                    {
                        Logger.WriteDebug($"Client {polledPlayer} has connected previously under a different ip/name");
                        client.CurrentAlias = new EFAlias()
                        {
                            IPAddress = polledPlayer.IPAddress,
                            Name = polledPlayer.Name,
                        };
                        // we need to update their new ip and name to the virtual property
                        client.Name = polledPlayer.Name;
                        client.IPAddress = polledPlayer.IPAddress;

                        await Manager.GetClientService().Update(client);
                    }

                    else if (existingAlias.Name == polledPlayer.Name)
                    {
                        client.CurrentAlias = existingAlias;
                        client.CurrentAliasId = existingAlias.AliasId;
                        await Manager.GetClientService().Update(client);
                    }
                    player = client.AsPlayer();
                }

                // Do the player specific stuff
                player.ClientNumber = polledPlayer.ClientNumber;
                player.Score = polledPlayer.Score;
                player.CurrentServer = this;
                Players[player.ClientNumber] = player;

                var activePenalties = await Manager.GetPenaltyService().GetActivePenaltiesAsync(player.AliasLinkId, player.IPAddress);
                var currentBan = activePenalties.FirstOrDefault(b => b.Expires > DateTime.UtcNow);

                if (currentBan != null)
                {
                    Logger.WriteInfo($"Banned client {player} trying to connect...");
                    var autoKickClient = (await Manager.GetClientService().Get(1)).AsPlayer();
                    autoKickClient.CurrentServer = this;

                    if (currentBan.Type == Penalty.PenaltyType.TempBan)
                    {
                        string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, $"You are temporarily banned. ({(currentBan.Expires - DateTime.UtcNow).TimeSpanText()} left)");
                        await this.ExecuteCommandAsync(formattedKick);
                    }
                    else
                        await player.Kick($"Previously banned for {currentBan.Offense}", autoKickClient);

                    if (player.Level != Player.Permission.Banned && currentBan.Type == Penalty.PenaltyType.Ban)
                        await player.Ban($"Previously banned for {currentBan.Offense}", autoKickClient);
                    return true;
                }

                Logger.WriteInfo($"Client {player} connecting...");

                await ExecuteEvent(new GameEvent(GameEvent.EventType.Connect, "", player, null, this));


                if (!Manager.GetApplicationSettings().Configuration().EnableClientVPNs &&
                    await VPNCheck.UsingVPN(player.IPAddressString, Manager.GetApplicationSettings().Configuration().IPHubAPIKey))
                {
                    await player.Kick("VPNs are not allowed on this server", new Player() { ClientId = 1 });
                }

                return true;
            }

            catch (Exception E)
            {
                Manager.GetLogger().WriteError($"Unable to add player {polledPlayer.Name}::{polledPlayer.NetworkId}");
                Manager.GetLogger().WriteDebug(E.StackTrace);
                return false;
            }
        }

        //Remove player by CLIENT NUMBER
        override public async Task RemovePlayer(int cNum)
        {
            if (cNum >= 0 && Players[cNum] != null)
            {
                Player Leaving = Players[cNum];
                Logger.WriteInfo($"Client {Leaving} disconnecting...");

                await ExecuteEvent(new GameEvent(GameEvent.EventType.Disconnect, "", Leaving, null, this));

                Leaving.TotalConnectionTime += (int)(DateTime.UtcNow - Leaving.ConnectionTime).TotalSeconds;
                Leaving.LastConnection = DateTime.UtcNow;
                await Manager.GetClientService().Update(Leaving);
                Players[cNum] = null;
            }
        }

        //Process requested command correlating to an event
        // todo: this needs to be removed out of here
        override public async Task<Command> ValidateCommand(GameEvent E)
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
                throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} entered unknown command \"{CommandString}\"");
            }

            E.Data = E.Data.RemoveWords(1);
            String[] Args = E.Data.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (E.Origin.Level < C.Permission)
            {
                await E.Origin.Tell("You do not have access to that command");
                throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} does not have access to \"{C.Name}\"");
            }

            if (Args.Length < (C.RequiredArgumentCount))
            {
                await E.Origin.Tell($"Not enough arguments supplied");
                await E.Origin.Tell(C.Syntax);
                throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
            }

            if (C.RequiresTarget || Args.Length > 0)
            {
                int cNum = -1;
                try
                {
                    cNum = Convert.ToInt32(Args[0]);
                }

                catch (FormatException)
                {

                }

                if (Args[0][0] == '@') // user specifying target by database ID
                {
                    int dbID = -1;
                    int.TryParse(Args[0].Substring(1, Args[0].Length - 1), out dbID);

                    var found = await Manager.GetClientService().Get(dbID);
                    if (found != null)
                    {
                        E.Target = found.AsPlayer();
                        E.Target.CurrentServer = this as IW4MServer;
                        E.Owner = this as IW4MServer;
                        E.Data = String.Join(" ", Args.Skip(1));
                    }
                }

                else if (Args[0].Length < 3 && cNum > -1 && cNum < MaxClients) // user specifying target by client num
                {
                    if (Players[cNum] != null)
                    {
                        E.Target = Players[cNum];
                        E.Data = String.Join(" ", Args.Skip(1));
                    }
                }

                List<Player> matchingPlayers;

                if (E.Target == null) // Find active player including quotes (multiple words)
                {
                    matchingPlayers = GetClientByName(E.Data.Trim());
                    if (matchingPlayers.Count > 1)
                    {
                        await E.Origin.Tell("Multiple players match that name");
                        throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} had multiple players found for {C.Name}");
                    }
                    else if (matchingPlayers.Count == 1)
                    {
                        E.Target = matchingPlayers.First();

                        string escapedName = Regex.Escape(E.Target.Name);
                        var reg = new Regex($"(\"{escapedName}\")|({escapedName})", RegexOptions.IgnoreCase);
                        E.Data = reg.Replace(E.Data, "", 1).Trim();

                        if (E.Data.Length == 0 && C.RequiredArgumentCount > 1)
                        {
                            await E.Origin.Tell($"Not enough arguments supplied!");
                            await E.Origin.Tell(C.Syntax);
                            throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
                        }
                    }
                }

                if (E.Target == null) // Find active player as single word
                {
                    matchingPlayers = GetClientByName(Args[0]);
                    if (matchingPlayers.Count > 1)
                    {
                        await E.Origin.Tell("Multiple players match that name");
                        foreach (var p in matchingPlayers)
                            await E.Origin.Tell($"[^3{p.ClientNumber}^7] {p.Name}");
                        throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} had multiple players found for {C.Name}");
                    }
                    else if (matchingPlayers.Count == 1)
                    {
                        E.Target = matchingPlayers.First();

                        string escapedName = Regex.Escape(E.Target.Name);
                        string escapedArg = Regex.Escape(Args[0]);
                        var reg = new Regex($"({escapedName})|({escapedArg})", RegexOptions.IgnoreCase);
                        E.Data = reg.Replace(E.Data, "", 1).Trim();

                        if ((E.Data.Trim() == E.Target.Name.ToLower().Trim() ||
                            E.Data == String.Empty) &&
                            C.RequiresTarget)
                        {
                            await E.Origin.Tell($"Not enough arguments supplied!");
                            await E.Origin.Tell(C.Syntax);
                            throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
                        }
                    }
                }

                if (E.Target == null && C.RequiresTarget)
                {
                    await E.Origin.Tell("Unable to find specified player.");
                    throw new SharedLibraryCore.Exceptions.CommandException($"{E.Origin} specified invalid player for \"{C.Name}\"");
                }
            }
            E.Data = E.Data.Trim();
            return C;
        }

        public override async Task ExecuteEvent(GameEvent E)
        {
            if (Throttled)
                return;

            await ProcessEvent(E);
            Manager.GetEventApi().OnServerEvent(this, E);

            foreach (IPlugin P in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
            {
#if !DEBUG
                try
#endif
                {
                    if (cts.IsCancellationRequested)
                        break;

                    await P.OnEventAsync(E, this);
                }
#if !DEBUG
                catch (Exception Except)
                {
                    Logger.WriteError(String.Format("The plugin \"{0}\" generated an error. ( see log )", P.Name));
                    Logger.WriteDebug(String.Format("Error Message: {0}", Except.Message));
                    Logger.WriteDebug(String.Format("Error Trace: {0}", Except.StackTrace));
                    while (Except.InnerException != null)
                    {
                        Except = Except.InnerException;
                        Logger.WriteDebug($"Inner exception: {Except.Message}");
                    }
                    continue;
                }
#endif
            }
        }

        async Task<int> PollPlayersAsync()
        {
            var now = DateTime.Now;

            List<Player> CurrentPlayers = null;
            try
            {
                CurrentPlayers = await this.GetStatusAsync();
            }

            // when the server has lost connection
            catch (SharedLibraryCore.Exceptions.NetworkException)
            {
                Throttled = true;
                return ClientNum;
            }
#if DEBUG
            Logger.WriteInfo($"Polling players took {(DateTime.Now - now).TotalMilliseconds}ms");
#endif
            Throttled = false;
            for (int i = 0; i < Players.Count; i++)
            {
                if (CurrentPlayers.Find(p => p.ClientNumber == i) == null && Players[i] != null)
                    await RemovePlayer(i);
            }

            for (int i = 0; i < CurrentPlayers.Count; i++)
            {
                await AddPlayer(CurrentPlayers[i]);
            }

            return CurrentPlayers.Count;
        }

        long l_size = -1;
        String[] lines = new String[8];
        String[] oldLines = new String[8];
        DateTime start = DateTime.Now;
        DateTime playerCountStart = DateTime.Now;
        DateTime lastCount = DateTime.Now;
        DateTime tickTime = DateTime.Now;
        bool firstRun = true;

        override public async Task<bool> ProcessUpdatesAsync(CancellationToken cts)
        {
            this.cts = cts;
            //#if DEBUG == false
            try
            //#endif
            {
                // first start
                if (firstRun)
                {
                    await ExecuteEvent(new GameEvent(GameEvent.EventType.Start, "Server started", null, null, this));
                    firstRun = false;
                }

                if ((DateTime.Now - LastPoll).TotalMinutes < 2 && ConnectionErrors >= 1)
                    return true;

                try
                {
                    int polledPlayerCount = await PollPlayersAsync();

                    if (ConnectionErrors > 0)
                    {
                        Logger.WriteVerbose($"Connection has been reestablished with {IP}:{Port}");
                        Throttled = false;
                    }
                    ConnectionErrors = 0;
                    LastPoll = DateTime.Now;
                }

                catch (SharedLibraryCore.Exceptions.NetworkException e)
                {
                    ConnectionErrors++;
                    if (ConnectionErrors == 1)
                    {
                        Logger.WriteError($"{e.Message} {IP}:{Port}, reducing polling rate");
                        Logger.WriteDebug($"Internal Exception: {e.Data["internal_exception"]}");
                        Throttled = true;
                    }
                    return true;
                }

                LastMessage = DateTime.Now - start;
                lastCount = DateTime.Now;

                if ((DateTime.Now - tickTime).TotalMilliseconds >= 1000)
                {
                    foreach (var Plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
                    {
                        if (cts.IsCancellationRequested)
                            break;

                        await Plugin.OnTickAsync(this);
                    }
                    tickTime = DateTime.Now;
                }

                if ((lastCount - playerCountStart).TotalMinutes >= SharedLibraryCore.Helpers.PlayerHistory.UpdateInterval)
                {
                    while (PlayerHistory.Count > ((60 / SharedLibraryCore.Helpers.PlayerHistory.UpdateInterval) * 12)) // 12 times a hour for 12 hours
                        PlayerHistory.Dequeue();
                    PlayerHistory.Enqueue(new SharedLibraryCore.Helpers.PlayerHistory(ClientNum));
                    playerCountStart = DateTime.Now;
                }

                if (LastMessage.TotalSeconds > Manager.GetApplicationSettings().Configuration().AutoMessagePeriod
                    && BroadcastMessages.Count > 0
                    && ClientNum > 0)
                {
                    await Broadcast(Utilities.ProcessMessageToken(Manager.GetMessageTokens(), BroadcastMessages[NextMessage]));
                    NextMessage = NextMessage == (BroadcastMessages.Count - 1) ? 0 : NextMessage + 1;
                    start = DateTime.Now;
                }

                if (LogFile == null)
                    return true;

                if (l_size != LogFile.Length())
                {
                    // this should be the longest running task
                    await Task.FromResult(lines = LogFile.Tail(12));
                    if (lines != oldLines)
                    {
                        l_size = LogFile.Length();
                        int end = (lines.Length == oldLines.Length) ? lines.Length - 1 : Math.Abs((lines.Length - oldLines.Length)) - 1;

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
                                GameEvent event_ = EventParser.GetEvent(this, lines[count]);
                                if (event_ != null)
                                {
                                    if (event_.Origin == null)
                                        continue;

                                    await ExecuteEvent(event_);
                                }
                            }
                        }
                    }
                }
                oldLines = lines;
                l_size = LogFile.Length();
                if (Manager.ShutdownRequested())
                {
                    foreach (var plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
                        await plugin.OnUnloadAsync();

                    for (int i = 0; i < Players.Count; i++)
                        await RemovePlayer(i);
                }
                return true;
            }
            //#if !DEBUG
            catch (SharedLibraryCore.Exceptions.NetworkException)
            {
                Logger.WriteError($"Could not communicate with {IP}:{Port}");
                return false;
            }

            catch (Exception E)
            {
                Logger.WriteError($"Encountered error on {IP}:{Port}");
                Logger.WriteDebug("Error Message: " + E.Message);
                Logger.WriteDebug("Error Trace: " + E.StackTrace);
                return false;
            }
            //#endif
        }

        public async Task Initialize()
        {
            RconParser = ServerConfig.UseT6MParser ? (IRConParser)new T6MRConParser() : new IW4RConParser();
            EventParser = ServerConfig.UseT6MParser ? (IEventParser)new T6MEventParser() : new IW4EventParser();

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
            WorkingDirectory = basepath.Value;
            var game = await this.GetDvarAsync<string>("fs_game");
            var logfile = await this.GetDvarAsync<string>("g_log");
            var logsync = await this.GetDvarAsync<int>("g_logsync");

            Dvar<int> onelog = null;
            if (GameName == Game.IW4)
            {
                try
                {
                    onelog = await this.GetDvarAsync<int>("iw4x_onelog");
                }

                catch (Exception)
                {
                    onelog = new Dvar<int>("iw4x_onelog")
                    {
                        Value = -1
                    };
                }
            }

            try
            {
                var website = await this.GetDvarAsync<string>("_website");
                Website = website.Value;
            }

            catch (SharedLibraryCore.Exceptions.DvarException)
            {
                Website = "this server's website";
            }

            InitializeMaps();

            this.Hostname = hostname.Value.StripColors();
            this.CurrentMap = Maps.Find(m => m.Name == mapname.Value) ?? new Map() { Alias = mapname.Value, Name = mapname.Value };
            this.MaxClients = maxplayers.Value;
            this.FSGame = game.Value;

            await this.SetDvarAsync("sv_kickbantime", 60);

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

            string mainPath = (GameName == Game.IW4 && onelog.Value >= 0) ? "userraw" : "main";
            // patch for T5M:V2 log path
            mainPath = (GameName == Game.T5M) ? "rzodemo" : mainPath;
            // patch for T6M:PLUTONIUM
            mainPath = (GameName == Game.T6M) ? $"t6r{Path.DirectorySeparatorChar}data" : mainPath;
#if DEBUG
            basepath.Value = @"\\192.168.88.253\Call of Duty Black Ops II";
#endif
            string logPath = (game.Value == "" || onelog?.Value == 1) ?
                $"{basepath.Value.Replace('\\', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{mainPath}{Path.DirectorySeparatorChar}{logfile.Value}" :
                $"{basepath.Value.Replace('\\', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{game.Value.Replace('/', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{logfile.Value}";

            if (!File.Exists(logPath))
            {
                Logger.WriteError($"Gamelog {logPath} does not exist!");
#if !DEBUG
                throw new SharedLibraryCore.Exceptions.ServerException($"Invalid gamelog file {logPath}");
#endif
            }
            else
            {
                LogFile = new IFile(logPath);
            }

            Logger.WriteInfo($"Log file is {logPath}");
#if DEBUG
        //    LogFile = new RemoteFile("https://raidmax.org/IW4MAdmin/getlog.php");
#else
            await Broadcast("IW4M Admin is now ^2ONLINE");
#endif
        }

        //Process any server event
        override protected async Task ProcessEvent(GameEvent E)
        {
            if (E.Type == GameEvent.EventType.Connect)
            {
                ChatHistory.Add(new ChatInfo()
                {
                    Name = E.Origin.Name,
                    Message = "CONNECTED",
                    Time = DateTime.UtcNow
                });

                if (E.Origin.Level > Player.Permission.Moderator)
                    await E.Origin.Tell($"There are ^5{Reports.Count} ^7recent reports");
            }

            else if (E.Type == GameEvent.EventType.Disconnect)
            {
                ChatHistory.Add(new ChatInfo()
                {
                    Name = E.Origin.Name,
                    Message = "DISCONNECTED",
                    Time = DateTime.UtcNow
                });
            }

            else if (E.Type == GameEvent.EventType.Script)
            {
                await ExecuteEvent(new GameEvent(GameEvent.EventType.Kill, E.Data, E.Origin, E.Target, this));
            }

            if (E.Type == GameEvent.EventType.Say && E.Data.Length >= 2)
            {
                if (E.Data.Substring(0, 1) == "!" || E.Data.Substring(0, 1) == "@" || E.Origin.Level == Player.Permission.Console)
                {
                    Command C = null;

                    try
                    {
                        C = await ValidateCommand(E);
                    }

                    catch (SharedLibraryCore.Exceptions.CommandException e)
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
                    E.Data = E.Data.StripColors();

                    ChatHistory.Add(new ChatInfo()
                    {
                        Name = E.Origin.Name,
                        Message = E.Data,
                        Time = DateTime.UtcNow
                    });
                }
            }

            if (E.Type == GameEvent.EventType.MapChange)
            {
                Logger.WriteInfo($"New map loaded - {ClientNum} active players");

                Gametype = (await this.GetDvarAsync<string>("g_gametype")).Value.StripColors();
                Hostname = (await this.GetDvarAsync<string>("sv_hostname")).Value.StripColors();
                FSGame = (await this.GetDvarAsync<string>("fs_game")).Value.StripColors();

                string mapname = this.GetDvarAsync<string>("mapname").Result.Value;
                CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map() { Alias = mapname, Name = mapname };
            }

            if (E.Type == GameEvent.EventType.MapEnd)
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
            // ensure player gets warned if command not performed on them in game
            if (Target.ClientNumber < 0)
            {
                var ingameClient = Manager.GetActiveClients()
                    .FirstOrDefault(c => c.ClientId == Target.ClientId);

                if (ingameClient != null)
                {
                    await Warn(Reason, ingameClient, Origin);
                    return;
                }
            }

            else
            {
                if (Target.Warnings >= 4)
                {
                    await Target.Kick("Too many warnings!", (await Manager.GetClientService().Get(1)).AsPlayer());
                    return;
                }

                Target.Warnings++;
                String Message = String.Format("^1WARNING ^7[^3{0}^7]: ^3{1}^7, {2}", Target.Warnings, Target.Name, Reason);
                await Target.CurrentServer.Broadcast(Message);
            }

            Penalty newPenalty = new Penalty()
            {
                Type = Penalty.PenaltyType.Warning,
                Expires = DateTime.UtcNow,
                Offender = Target,
                Offense = Reason,
                Punisher = Origin,
                Active = true,
                When = DateTime.UtcNow,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        public override async Task Kick(String Reason, Player Target, Player Origin)
        {
            // ensure player gets kicked if command not performed on them in game
            if (Target.ClientNumber < 0)
            {
                var ingameClient = Manager.GetActiveClients()
                     .FirstOrDefault(c => c.ClientId == Target.ClientId);

                if (ingameClient != null)
                {
                    await Kick(Reason, ingameClient, Origin);
                    return;
                }
            }
#if !DEBUG
            else
            {
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, Target.ClientNumber, $"You were Kicked - ^5{Reason}^7");
                await Target.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
#endif

#if DEBUG
            await Target.CurrentServer.RemovePlayer(Target.ClientNumber);
#endif

            var newPenalty = new Penalty()
            {
                Type = Penalty.PenaltyType.Kick,
                Expires = DateTime.UtcNow,
                Offender = Target,
                Offense = Reason,
                Punisher = Origin,
                Active = true,
                When = DateTime.UtcNow,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        public override async Task TempBan(String Reason, TimeSpan length, Player Target, Player Origin)
        {
            // ensure player gets banned if command not performed on them in game
            if (Target.ClientNumber < 0)
            {
                var ingameClient = Manager.GetActiveClients()
                     .FirstOrDefault(c => c.ClientId == Target.ClientId);

                if (ingameClient != null)
                {
                    await TempBan(Reason, length, ingameClient, Origin);
                    return;
                }
            }
#if !DEBUG
            else
            {
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, Target.ClientNumber, $"^7You're Temporarily Banned - ^5{Reason}");
                await Target.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
#else
            await Target.CurrentServer.RemovePlayer(Target.ClientNumber);
#endif

            Penalty newPenalty = new Penalty()
            {
                Type = Penalty.PenaltyType.TempBan,
                Expires = DateTime.UtcNow + length,
                Offender = Target,
                Offense = Reason,
                Punisher = Origin,
                Active = true,
                When = DateTime.UtcNow,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        override public async Task Ban(String Message, Player Target, Player Origin)
        {
            // ensure player gets banned if command not performed on them in game
            if (Target.ClientNumber < 0)
            {
                Player ingameClient = null;

                ingameClient = Manager.GetServers()
                    .Select(s => s.GetPlayersAsList())
                    .FirstOrDefault(l => l.FirstOrDefault(c => c.ClientId == Target.ClientId) != null)
                    ?.First(c => c.ClientId == Target.ClientId);

                if (ingameClient != null)
                {
                    await Ban(Message, ingameClient, Origin);
                    return;
                }
            }

            else
            {
                // this is set only because they're still in the server.
                Target.Level = Player.Permission.Banned;
#if !DEBUG
                string formattedString = String.Format(RconParser.GetCommandPrefixes().Kick, Target.ClientNumber, $"You're Banned - ^5{Message} ^7(appeal at {Website})^7");
                await Target.CurrentServer.ExecuteCommandAsync(formattedString);
#else
                await Target.CurrentServer.RemovePlayer(Target.ClientNumber);
#endif
            }

            Penalty newPenalty = new Penalty()
            {
                Type = Penalty.PenaltyType.Ban,
                Expires = DateTime.MaxValue,
                Offender = Target,
                Offense = Message,
                Punisher = Origin,
                Active = true,
                When = DateTime.UtcNow,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        override public async Task Unban(string reason, Player Target, Player Origin)
        {
            var unbanPenalty = new Penalty()
            {
                Type = Penalty.PenaltyType.Unban,
                Expires = DateTime.UtcNow,
                Offender = Target,
                Offense = reason,
                Punisher = Origin,
                When = DateTime.UtcNow,
                Active = true,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(unbanPenalty);
            await Manager.GetPenaltyService().RemoveActivePenalties(Target.AliasLink.AliasLinkId);
        }

        override public void InitializeTokens()
        {
            Manager.GetMessageTokens().Add(new SharedLibraryCore.Helpers.MessageToken("TOTALPLAYERS", Manager.GetClientService().GetTotalClientsAsync().Result.ToString));
            Manager.GetMessageTokens().Add(new SharedLibraryCore.Helpers.MessageToken("VERSION", Application.Program.Version.ToString));
        }
    }
}
