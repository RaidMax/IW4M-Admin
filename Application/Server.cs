using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Exceptions;

using Application.Misc;
using Application.RconParsers;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.IO;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        private CancellationToken cts;
        private static Dictionary<string, string> loc = Utilities.CurrentLocalization.LocalizationSet;
        private GameLogEvent LogEvent;


        public IW4MServer(IManager mgr, ServerConfiguration cfg) : base(mgr, cfg) { }

        public override int GetHashCode()
        {
            // todo: make this better with collisions
            int id = Math.Abs($"{IP}:{Port.ToString()}".Select(a => (int)a).Sum());

            // hack: this is a nasty fix for get hashcode being changed
            switch (id)
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

            if ((polledPlayer.Ping == 999 && !polledPlayer.IsBot) ||
                polledPlayer.Ping < 1 ||
                polledPlayer.ClientNumber < 0)
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
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, loc["SERVER_KICK_MINNAME"]);
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

            if (Players.FirstOrDefault(p => p != null && p.Name == polledPlayer.Name) != null)
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their name is already in use");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, loc["SERVER_KICK_NAME_INUSE"]);
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

            if (polledPlayer.Name == "Unknown Soldier" ||
                polledPlayer.Name == "UnknownSoldier" ||
                polledPlayer.Name == "CHEATER")
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their name is generic");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, loc["SERVER_KICK_GENERICNAME"]);
                await this.ExecuteCommandAsync(formattedKick);
                return false;
            }

            if (polledPlayer.Name.Where(c => Char.IsControl(c)).Count() > 0)
            {
                Logger.WriteDebug($"Kicking {polledPlayer} because their name contains control characters");
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, loc["SERVER_KICK_CONTROLCHARS"]);
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
                player.IsBot = polledPlayer.IsBot;
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
                        string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, polledPlayer.ClientNumber, $"{loc["SERVER_TB_REMAIN"]} ({(currentBan.Expires - DateTime.UtcNow).TimeSpanText()} left)");
                        await this.ExecuteCommandAsync(formattedKick);
                    }
                    else
                        await player.Kick($"{loc["SERVER_BAN_PREV"]} {currentBan.Offense}", autoKickClient);

                    if (player.Level != Player.Permission.Banned && currentBan.Type == Penalty.PenaltyType.Ban)
                        await player.Ban($"{loc["SERVER_BAN_PREV"]} {currentBan.Offense}", autoKickClient);

                    // they didn't fully connect so empty their slot
                    Players[player.ClientNumber] = null;
                    return true;
                }

                Logger.WriteInfo($"Client {player} connecting...");

                var e = new GameEvent(GameEvent.EventType.Connect, "", player, null, this);
                Manager.GetEventHandler().AddEvent(e);

                e.OnProcessed.Wait();

                if (!Manager.GetApplicationSettings().Configuration().EnableClientVPNs &&
                    await VPNCheck.UsingVPN(player.IPAddressString, Manager.GetApplicationSettings().Configuration().IPHubAPIKey))
                {
                    await player.Kick(Utilities.CurrentLocalization.LocalizationSet["SERVER_KICK_VPNS_NOTALLOWED"], new Player() { ClientId = 1 });
                }

                return true;
            }

            catch (Exception E)
            {
                Manager.GetLogger().WriteError($"{loc["SERVER_ERROR_ADDPLAYER"]} {polledPlayer.Name}::{polledPlayer.NetworkId}");
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

                var e = new GameEvent(GameEvent.EventType.Disconnect, "", Leaving, null, this);
                Manager.GetEventHandler().AddEvent(e);
                e.OnProcessed.Wait();

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
                await E.Origin.Tell(loc["COMMAND_UNKNOWN"]);
                throw new CommandException($"{E.Origin} entered unknown command \"{CommandString}\"");
            }

            E.Data = E.Data.RemoveWords(1);
            String[] Args = E.Data.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (E.Origin.Level < C.Permission)
            {
                await E.Origin.Tell(loc["COMMAND_NOACCESS"]);
                throw new CommandException($"{E.Origin} does not have access to \"{C.Name}\"");
            }

            if (Args.Length < (C.RequiredArgumentCount))
            {
                await E.Origin.Tell(loc["COMMAND_MISSINGARGS"]);
                await E.Origin.Tell(C.Syntax);
                throw new CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
            }

            if (C.RequiresTarget || Args.Length > 0)
            {
                if (!Int32.TryParse(Args[0], out int cNum))
                    cNum = -1;

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
                        await E.Origin.Tell(loc["COMMAND_TARGET_MULTI"]);
                        throw new CommandException($"{E.Origin} had multiple players found for {C.Name}");
                    }
                    else if (matchingPlayers.Count == 1)
                    {
                        E.Target = matchingPlayers.First();

                        string escapedName = Regex.Escape(E.Target.Name);
                        var reg = new Regex($"(\"{escapedName}\")|({escapedName})", RegexOptions.IgnoreCase);
                        E.Data = reg.Replace(E.Data, "", 1).Trim();

                        if (E.Data.Length == 0 && C.RequiredArgumentCount > 1)
                        {
                            await E.Origin.Tell(loc["COMMAND_MISSINGARGS"]);
                            await E.Origin.Tell(C.Syntax);
                            throw new CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
                        }
                    }
                }

                if (E.Target == null) // Find active player as single word
                {
                    matchingPlayers = GetClientByName(Args[0]);
                    if (matchingPlayers.Count > 1)
                    {
                        await E.Origin.Tell(loc["COMMAND_TARGET_MULTI"]);
                        foreach (var p in matchingPlayers)
                            await E.Origin.Tell($"[^3{p.ClientNumber}^7] {p.Name}");
                        throw new CommandException($"{E.Origin} had multiple players found for {C.Name}");
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
                            await E.Origin.Tell(loc["COMMAND_MISSINGARGS"]);
                            await E.Origin.Tell(C.Syntax);
                            throw new CommandException($"{E.Origin} did not supply enough arguments for \"{C.Name}\"");
                        }
                    }
                }

                if (E.Target == null && C.RequiresTarget)
                {
                    await E.Origin.Tell(loc["COMMAND_TARGET_NOTFOUND"]);
                    throw new CommandException($"{E.Origin} specified invalid player for \"{C.Name}\"");
                }
            }
            E.Data = E.Data.Trim();
            return C;
        }

        public override async Task ExecuteEvent(GameEvent E)
        {
            bool canExecuteCommand = true;
            await ProcessEvent(E);
            Manager.GetEventApi().OnServerEvent(this, E);

            // this allows us to catch exceptions but still run it parallel
            async Task pluginHandlingAsync(Task onEvent, string pluginName)
            {
                try
                {
                    if (cts.IsCancellationRequested)
                        return;

                    await onEvent;
                }

                // this happens if a plugin (login) wants to stop commands from executing
                catch (AuthorizationException e)
                {
                    await E.Origin.Tell($"{loc["COMMAND_NOTAUTHORIZED"]} - {e.Message}");
                    canExecuteCommand = false;
                }

                catch (Exception Except)
                {
                    Logger.WriteError($"{loc["SERVER_PLUGIN_ERROR"]} [{pluginName}]");
                    Logger.WriteDebug(String.Format("Error Message: {0}", Except.Message));
                    Logger.WriteDebug(String.Format("Error Trace: {0}", Except.StackTrace));
                    while (Except.InnerException != null)
                    {
                        Except = Except.InnerException;
                        Logger.WriteDebug($"Inner exception: {Except.Message}");
                    }
                }
            }

            var pluginTasks = SharedLibraryCore.Plugins.PluginImporter.ActivePlugins.
                Select(p => pluginHandlingAsync(p.OnEventAsync(E, this), p.Name));

            // execute all the plugin updates simultaneously
            await Task.WhenAll(pluginTasks);

            // hack: this prevents commands from getting executing that 'shouldn't' be
            if (E.Type == GameEvent.EventType.Command &&
                E.Extra != null &&
                (canExecuteCommand ||
                E.Origin?.Level == Player.Permission.Console))
            {
                await (((Command)E.Extra).ExecuteAsync(E));
            }
        }

        /// <summary>
        /// Perform the server specific tasks when an event occurs 
        /// </summary>
        /// <param name="E"></param>
        /// <returns></returns>
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
                    await E.Origin.Tell(string.Format(loc["SERVER_REPORT_COUNT"], E.Owner.Reports.Count));
            }

            else if (E.Type == GameEvent.EventType.Join)
            {
                // special case for IW5 when connect is from the log
                if (E.Extra != null && GameName == Game.IW5)
                {
                    var logClient = (Player)E.Extra;
                    var client = (await this.GetStatusAsync())
                        .Single(c => c.ClientNumber == logClient.ClientNumber &&
                        c.Name == logClient.Name);
                    client.NetworkId = logClient.NetworkId;

                    await AddPlayer(client);
                }
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
                Manager.GetEventHandler().AddEvent(GameEvent.TranferWaiter(GameEvent.EventType.Kill, E));
            }

            if (E.Type == GameEvent.EventType.Say && E.Data.Length >= 2)
            {
                if (E.Data.Substring(0, 1) == "!" ||
                    E.Data.Substring(0, 1) == "@" ||
                    E.Origin.Level == Player.Permission.Console)
                {
                    Command C = null;

                    try
                    {
                        C = await ValidateCommand(E);
                    }

                    catch (CommandException e)
                    {
                        Logger.WriteInfo(e.Message);
                    }

                    if (C != null)
                    {
                        if (C.RequiresTarget && E.Target == null)
                        {
                            Logger.WriteWarning("Requested event (command) requiring target does not have a target!");
                        }

                        E.Extra = C;



                        // reprocess event as a command
                        Manager.GetEventHandler().AddEvent(GameEvent.TranferWaiter(GameEvent.EventType.Command, E));
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

                // iw4 doesn't log the game info
                if (E.Extra == null)
                {
                    var dict = await this.GetInfoAsync();

                    Gametype = dict["gametype"].StripColors();
                    Hostname = dict["hostname"].StripColors();

                    string mapname = dict["mapname"].StripColors();
                    CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map() { Alias = mapname, Name = mapname };
                }

                else
                {
                    var dict = (Dictionary<string, string>)E.Extra;
                    Gametype = dict["g_gametype"].StripColors();
                    Hostname = dict["sv_hostname"].StripColors();

                    string mapname = dict["mapname"].StripColors();
                    CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map() { Alias = mapname, Name = mapname };
                }
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


        async Task<int> PollPlayersAsync()
        {
            var now = DateTime.Now;

            List<Player> CurrentPlayers = null;
            try
            {
                CurrentPlayers = await this.GetStatusAsync();
            }

            // when the server has lost connection
            catch (NetworkException)
            {
                Throttled = true;
                return ClientNum;
            }
#if DEBUG
            Logger.WriteInfo($"Polling players took {(DateTime.Now - now).TotalMilliseconds}ms");
#endif
            Throttled = false;

            var clients = GetPlayersAsList();
            foreach (var client in clients)
            {
                if (GameName == Game.IW5)
                {
                    if (!CurrentPlayers.Select(c => c.ClientNumber).Contains(client.ClientNumber))
                        await RemovePlayer(client.ClientNumber);
                }

                else
                {
                    if (!CurrentPlayers.Select(c => c.NetworkId).Contains(client.NetworkId))
                        await RemovePlayer(client.ClientNumber);
                }
            }

            for (int i = 0; i < CurrentPlayers.Count; i++)
            {
                // todo: wait til GUID is included in status to fix this
                if (GameName != Game.IW5)
                    await AddPlayer(CurrentPlayers[i]);
            }

            return CurrentPlayers.Count;
        }

        DateTime start = DateTime.Now;
        DateTime playerCountStart = DateTime.Now;
        DateTime lastCount = DateTime.Now;
        DateTime tickTime = DateTime.Now;

        override public async Task<bool> ProcessUpdatesAsync(CancellationToken cts)
        {
            // this isn't really used anymore
            this.cts = cts;

            try
            {
                if (Manager.ShutdownRequested())
                {
                    foreach (var plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
                        await plugin.OnUnloadAsync();

                    for (int i = 0; i < Players.Count; i++)
                        await RemovePlayer(i);
                }

                // only check every 2 minutes if the server doesn't seem to be responding
                if ((DateTime.Now - LastPoll).TotalMinutes < 2 && ConnectionErrors >= 1)
                    return true;

                try
                {
                    int polledPlayerCount = await PollPlayersAsync();

                    if (ConnectionErrors > 0)
                    {
                        Logger.WriteVerbose($"{loc["MANAGER_CONNECTION_REST"]} {IP}:{Port}");
                        Throttled = false;
                    }
                    ConnectionErrors = 0;
                    LastPoll = DateTime.Now;
                }

                catch (NetworkException e)
                {
                    ConnectionErrors++;
                    if (ConnectionErrors == 1)
                    {
                        Logger.WriteError($"{e.Message} {IP}:{Port}, {loc["SERVER_ERROR_POLLING"]}");
                        Logger.WriteDebug($"Internal Exception: {e.Data["internal_exception"]}");
                        Throttled = true;
                    }
                    return true;
                }

                LastMessage = DateTime.Now - start;
                lastCount = DateTime.Now;

                // todo: re-enable on tick
                /*
                if ((DateTime.Now - tickTime).TotalMilliseconds >= 1000)
                {
                    foreach (var Plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
                    {
                        if (cts.IsCancellationRequested)
                            break;

                        await Plugin.OnTickAsync(this);
                    }
                    tickTime = DateTime.Now;
                }*/

                // update the player history 
                if ((lastCount - playerCountStart).TotalMinutes >= SharedLibraryCore.Helpers.PlayerHistory.UpdateInterval)
                {
                    while (PlayerHistory.Count > ((60 / SharedLibraryCore.Helpers.PlayerHistory.UpdateInterval) * 12)) // 12 times a hour for 12 hours
                        PlayerHistory.Dequeue();
                    PlayerHistory.Enqueue(new SharedLibraryCore.Helpers.PlayerHistory(ClientNum));
                    playerCountStart = DateTime.Now;
                }

                // send out broadcast messages
                if (LastMessage.TotalSeconds > Manager.GetApplicationSettings().Configuration().AutoMessagePeriod
                    && BroadcastMessages.Count > 0
                    && ClientNum > 0)
                {
                    await Broadcast(Utilities.ProcessMessageToken(Manager.GetMessageTokens(), BroadcastMessages[NextMessage]));
                    NextMessage = NextMessage == (BroadcastMessages.Count - 1) ? 0 : NextMessage + 1;
                    start = DateTime.Now;
                }

                return true;
            }

            // this one is ok
            catch (ServerException e)
            {
                if (e is NetworkException)
                {
                    Logger.WriteError($"{loc["SERVER_ERROR_COMMUNICATION"]} {IP}:{Port}");
                }

                return false;
            }

            catch (Exception E)
            {
                Logger.WriteError($"{loc["SERVER_ERROR_EXCEPTION"]} {IP}:{Port}");
                Logger.WriteDebug("Error Message: " + E.Message);
                Logger.WriteDebug("Error Trace: " + E.StackTrace);
                return false;
            }
        }

        public async Task Initialize()
        {
            RconParser = ServerConfig.UseT6MParser ? (IRConParser)new T6MRConParser() : new IW4RConParser();
            if (ServerConfig.UseIW5MParser)
                RconParser = new IW5MRConParser();


            var version = await this.GetDvarAsync<string>("version");
            GameName = Utilities.GetGame(version.Value);

            if (GameName == Game.IW4)
                EventParser = new IW4EventParser();
            else if (GameName == Game.IW5)
                EventParser = new IW5EventParser();
            else if (GameName == Game.T5M)
                EventParser = new T5MEventParser();
            else if (GameName == Game.T6M)
                EventParser = new T6MEventParser();
            else
                EventParser = new IW3EventParser(); // this uses the 'main' folder for log paths

            if (GameName == Game.UKN)
                Logger.WriteWarning($"Game name not recognized: {version}");

            var infoResponse = await this.GetInfoAsync();
            // this is normally slow, but I'm only doing it because different games have different prefixes
            var hostname = infoResponse == null ?
                (await this.GetDvarAsync<string>("sv_hostname")).Value :
                infoResponse.Where(kvp => kvp.Key.Contains("hostname")).Select(kvp => kvp.Value).First();
            var mapname = infoResponse == null ?
                (await this.GetDvarAsync<string>("mapname")).Value :
                infoResponse["mapname"];
            int maxplayers = (GameName == Game.IW4) ?  // gotta love IW4 idiosyncrasies
                (await this.GetDvarAsync<int>("party_maxplayers")).Value :
                infoResponse == null ?
                (await this.GetDvarAsync<int>("sv_maxclients")).Value :
                Convert.ToInt32(infoResponse["sv_maxclients"]);
            var gametype = infoResponse == null ?
                (await this.GetDvarAsync<string>("g_gametype")).Value :
                infoResponse.Where(kvp => kvp.Key.Contains("gametype")).Select(kvp => kvp.Value).First();
            var basepath = await this.GetDvarAsync<string>("fs_basepath");
            var game = infoResponse == null || !infoResponse.ContainsKey("fs_game") ?
                (await this.GetDvarAsync<string>("fs_game")).Value :
                infoResponse["fs_game"];
            var logfile = await this.GetDvarAsync<string>("g_log");
            var logsync = await this.GetDvarAsync<int>("g_logsync");

            WorkingDirectory = basepath.Value;

            try
            {
                var website = await this.GetDvarAsync<string>("_website");
                Website = website.Value;
            }

            catch (DvarException)
            {
                Website = loc["SERVER_WEBSITE_GENERIC"];
            }

            InitializeMaps();

            this.Hostname = hostname.StripColors();
            this.CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map() { Alias = mapname, Name = mapname };
            this.MaxClients = maxplayers;
            this.FSGame = game;
            this.Gametype = gametype;

            //wait this.SetDvarAsync("sv_kickbantime", 60);

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
            string mainPath = EventParser.GetGameDir();
#if DEBUG
            basepath.Value = @"\\192.168.88.253\mw2";
#endif
            string logPath;
            if (GameName == Game.IW5)
            {
                logPath = ServerConfig.ManualLogPath;
            }
            else
            {
                logPath = game == string.Empty ?
                    $"{basepath.Value.Replace('\\', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{mainPath}{Path.DirectorySeparatorChar}{logfile.Value}" :
                    $"{basepath.Value.Replace('\\', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{game.Replace('/', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{logfile.Value}";
            }


            // hopefully fix wine drive name mangling
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logPath = Regex.Replace(logPath, @"[A-Z]:", "");
            }

            if (!File.Exists(logPath))
            {
                Logger.WriteError($"{logPath} {loc["SERVER_ERROR_DNE"]}");
#if !DEBUG
                throw new ServerException($"{loc["SERVER_ERROR_LOG"]} {logPath}");
#endif
            }
            else
            {
                LogEvent = new GameLogEvent(this, logPath, logfile.Value);
            }

            Logger.WriteInfo($"Log file is {logPath}");
#if DEBUG
            // LogFile = new RemoteFile("https://raidmax.org/IW4MAdmin/getlog.php");
#else
            await Broadcast(loc["BROADCAST_ONLINE"]);
#endif
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
                    await Target.Kick(loc["SERVER_WARNLIMT_REACHED"], (await Manager.GetClientService().Get(1)).AsPlayer());
                    return;
                }

                Target.Warnings++;
                String Message = $"^1{loc["SERVER_WARNING"]} ^7[^3{Target.Warnings}^7]: ^3{Target.Name}^7, {Reason}";
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
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, Target.ClientNumber, $"{loc["SERVER_KICK_TEXT"]} - ^5{Reason}^7");
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
                string formattedKick = String.Format(RconParser.GetCommandPrefixes().Kick, Target.ClientNumber, $"^7{loc["SERVER_TB_TEXT"]}- ^5{Reason}");
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
                string formattedString = String.Format(RconParser.GetCommandPrefixes().Kick, Target.ClientNumber, $"{loc["SERVER_BAN_TEXT"]} - ^5{Message} ^7({loc["SERVER_BAN_APPEAL"]} {Website})^7");
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
            // prevent them from logging in again
            Manager.GetPrivilegedClients().Remove(Target.ClientId);
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

            await Manager.GetPenaltyService().RemoveActivePenalties(Target.AliasLink.AliasLinkId);
            await Manager.GetPenaltyService().Create(unbanPenalty);
        }

        override public void InitializeTokens()
        {
            Manager.GetMessageTokens().Add(new SharedLibraryCore.Helpers.MessageToken("TOTALPLAYERS", Manager.GetClientService().GetTotalClientsAsync().Result.ToString));
            Manager.GetMessageTokens().Add(new SharedLibraryCore.Helpers.MessageToken("VERSION", Application.Program.Version.ToString));
        }
    }
}
