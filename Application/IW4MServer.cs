using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.IO;
using IW4MAdmin.Application.RconParsers;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Localization;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        private static readonly Index loc = Utilities.CurrentLocalization.LocalizationIndex;
        private GameLogEventDetection LogEvent;

        public int Id { get; private set; }

        public IW4MServer(IManager mgr, ServerConfiguration cfg) : base(mgr, cfg)
        {
        }

        override public async Task OnClientConnected(EFClient clientFromLog)
        {
            Logger.WriteDebug($"Client slot #{clientFromLog.ClientNumber} now reserved");

            EFClient client = await Manager.GetClientService().GetUnique(clientFromLog.NetworkId);

            // first time client is connecting to server
            if (client == null)
            {
                Logger.WriteDebug($"Client {clientFromLog} first time connecting");
                clientFromLog.CurrentServer = this;
                client = await Manager.GetClientService().Create(clientFromLog);
            }

            /// this is only a temporary version until the IPAddress is transmitted
            client.CurrentAlias = new EFAlias()
            {
                Name = clientFromLog.Name,
                IPAddress = clientFromLog.IPAddress
            };

            Logger.WriteInfo($"Client {client} connected...");

            // Do the player specific stuff
            client.ClientNumber = clientFromLog.ClientNumber;
            client.Score = clientFromLog.Score;
            client.Ping = clientFromLog.Ping;
            client.CurrentServer = this;

            Clients[client.ClientNumber] = client;
#if DEBUG == true
            Logger.WriteDebug($"End PreConnect for {client}");
#endif
            var e = new GameEvent()
            {
                Origin = client,
                Owner = this,
                Type = GameEvent.EventType.Connect
            };

            await client.OnJoin(client.IPAddress);
            client.State = ClientState.Connected;
            Manager.GetEventHandler().AddEvent(e);
        }

        override public async Task OnClientDisconnected(EFClient client)
        {
#if DEBUG == true
            if (client.ClientNumber >= 0)
            {
#endif
            Logger.WriteInfo($"Client {client} [{client.State.ToString().ToLower()}] disconnecting...");
            Clients[client.ClientNumber] = null;
            await client.OnDisconnect();

            var e = new GameEvent()
            {
                Origin = client,
                Owner = this,
                Type = GameEvent.EventType.Disconnect
            };

            Manager.GetEventHandler().AddEvent(e);
#if DEBUG == true
            }
#endif
        }

        public override async Task ExecuteEvent(GameEvent E)
        {
            bool canExecuteCommand = true;

            if (!await ProcessEvent(E))
            {
                return;
            }

            Command C = null;
            if (E.Type == GameEvent.EventType.Command)
            {
                try
                {
                    C = await SharedLibraryCore.Commands.CommandProcessing.ValidateCommand(E);
                }

                catch (CommandException e)
                {
                    Logger.WriteInfo(e.Message);
                }

                if (C != null)
                {
                    E.Extra = C;
                }
            }

            foreach (var plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    await plugin.OnEventAsync(E, this);
                }
                catch (AuthorizationException e)
                {
                    E.Origin.Tell($"{loc["COMMAND_NOTAUTHORIZED"]} - {e.Message}");
                    canExecuteCommand = false;
                }
                catch (Exception Except)
                {
                    Logger.WriteError($"{loc["SERVER_PLUGIN_ERROR"]} [{plugin.Name}]");
                    Logger.WriteDebug(Except.GetExceptionInfo());
                }
            }

            // hack: this prevents commands from getting executing that 'shouldn't' be
            if (E.Type == GameEvent.EventType.Command &&
                E.Extra != null &&
                (canExecuteCommand ||
                E.Origin?.Level == EFClient.Permission.Console))
            {
                await (((Command)E.Extra).ExecuteAsync(E));
            }
        }

        /// <summary>
        /// Perform the server specific tasks when an event occurs 
        /// </summary>
        /// <param name="E"></param>
        /// <returns></returns>
        override protected async Task<bool> ProcessEvent(GameEvent E)
        {
#if DEBUG
            Logger.WriteDebug($"processing event of type {E.Type}");
#endif

            if (E.Type == GameEvent.EventType.ConnectionLost)
            {
                var exception = E.Extra as Exception;
                Logger.WriteError(exception.Message);
                if (exception.Data["internal_exception"] != null)
                {
                    Logger.WriteDebug($"Internal Exception: {exception.Data["internal_exception"]}");
                }
                Logger.WriteInfo("Connection lost to server, so we are throttling the poll rate");
                Throttled = true;
            }

            if (E.Type == GameEvent.EventType.ConnectionRestored)
            {
                if (Throttled)
                {
                    Logger.WriteVerbose(loc["MANAGER_CONNECTION_REST"].FormatExt($"[{IP}:{Port}]"));
                }
                Logger.WriteInfo("Connection restored to server, so we are no longer throttling the poll rate");
                Throttled = false;
            }

            if (E.Type == GameEvent.EventType.ChangePermission)
            {
                var newPermission = (Permission)E.Extra;
                Logger.WriteInfo($"{E.Origin} is setting {E.Target} to permission level {newPermission}");
                await Manager.GetClientService().UpdateLevel(newPermission, E.Target, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.PreConnect)
            {
                // we don't want to track bots in the database at all if ignore bots is requested
                if (E.Origin.IsBot && Manager.GetApplicationSettings().Configuration().IgnoreBots)
                {
                    return false;
                }

                var existingClient = GetClientsAsList().FirstOrDefault(_client => _client.Equals(E.Origin));

                // they're already connected
                if (existingClient != null && existingClient.ClientNumber == E.Origin.ClientNumber && !E.Origin.IsBot)
                {
                    Logger.WriteWarning($"detected preconnect for {E.Origin}, but they are already connected");
                    return false;
                }

                // this happens for some reason rarely where the client spots get out of order
                // possible a connect/reconnect game event before we get to process it here 
                else if (existingClient != null && existingClient.ClientNumber != E.Origin.ClientNumber)
                {
                    Logger.WriteWarning($"client {E.Origin} is trying to connect in client slot {E.Origin.ClientNumber}, but they are already registed in client slot {existingClient.ClientNumber}, swapping...");
                    // we need to remove them so the client spots can swap
                    await OnClientDisconnected(Clients[existingClient.ClientNumber]);
                }

                if (Clients[E.Origin.ClientNumber] == null)
                {
#if DEBUG == true
                    Logger.WriteDebug($"Begin PreConnect for {E.Origin}");
#endif
                    // we can go ahead and put them in so that they don't get re added
                    Clients[E.Origin.ClientNumber] = E.Origin;
                    try
                    {
                        await OnClientConnected(E.Origin);
                    }

                    catch (Exception ex)
                    {
                        Logger.WriteError($"{loc["SERVER_ERROR_ADDPLAYER"]} {E.Origin}");
                        Logger.WriteDebug(ex.GetExceptionInfo());

                        Clients[E.Origin.ClientNumber] = null;
                        return false;
                    }

                    ChatHistory.Add(new ChatInfo()
                    {
                        Name = E.Origin.Name,
                        Message = "CONNECTED",
                        Time = DateTime.UtcNow
                    });

                    if (E.Origin.Level > EFClient.Permission.Moderator)
                    {
                        E.Origin.Tell(string.Format(loc["SERVER_REPORT_COUNT"], E.Owner.Reports.Count));
                    }
                }

                // for some reason there's still a client in the spot
                else
                {
                    Logger.WriteWarning($"{E.Origin} is connecting but {Clients[E.Origin.ClientNumber]} is currently in that client slot");
                }
            }

            else if (E.Type == GameEvent.EventType.Flag)
            {
                // todo: maybe move this to a seperate function
                var newPenalty = new EFPenalty()
                {
                    Type = EFPenalty.PenaltyType.Flag,
                    Expires = DateTime.UtcNow,
                    Offender = E.Target,
                    Offense = E.Data,
                    Punisher = E.Origin,
                    When = DateTime.UtcNow,
                    Link = E.Target.AliasLink
                };

                var addedPenalty = await Manager.GetPenaltyService().Create(newPenalty);
                E.Target.SetLevel(Permission.Flagged, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.Unflag)
            {
                var unflagPenalty = new EFPenalty()
                {
                    Type = EFPenalty.PenaltyType.Unflag,
                    Expires = DateTime.UtcNow,
                    Offender = E.Target,
                    Offense = E.Data,
                    Punisher = E.Origin,
                    When = DateTime.UtcNow,
                    Link = E.Target.AliasLink
                };

                await Manager.GetPenaltyService().Create(unflagPenalty);
                E.Target.SetLevel(Permission.User, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.Report)
            {
                Reports.Add(new Report()
                {
                    Origin = E.Origin,
                    Target = E.Target,
                    Reason = E.Data
                });

                var newReport = new EFPenalty()
                {
                    Type = EFPenalty.PenaltyType.Report,
                    Expires = DateTime.UtcNow,
                    Offender = E.Target,
                    Offense = E.Message,
                    Punisher = E.Origin,
                    Active = true,
                    When = DateTime.UtcNow,
                    Link = E.Target.AliasLink
                };

                await Manager.GetPenaltyService().Create(newReport);
            }

            else if (E.Type == GameEvent.EventType.TempBan)
            {
                await TempBan(E.Data, (TimeSpan)E.Extra, E.Target, E.Origin); ;
            }

            else if (E.Type == GameEvent.EventType.Ban)
            {
                bool isEvade = E.Extra != null ? (bool)E.Extra : false;
                await Ban(E.Data, E.Target, E.Origin, isEvade);
            }

            else if (E.Type == GameEvent.EventType.Unban)
            {
                await Unban(E.Data, E.Target, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.Kick)
            {
                await Kick(E.Data, E.Target, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.Warn)
            {
                await Warn(E.Data, E.Target, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.Disconnect)
            {
                ChatHistory.Add(new ChatInfo()
                {
                    Name = E.Origin.Name,
                    Message = "DISCONNECTED",
                    Time = DateTime.UtcNow
                });

                await new MetaService().AddPersistentMeta("LastMapPlayed", CurrentMap.Alias, E.Origin);
                await new MetaService().AddPersistentMeta("LastServerPlayed", E.Owner.Hostname, E.Origin);
            }

            else if (E.Type == GameEvent.EventType.PreDisconnect)
            {
                // predisconnect comes from minimal rcon polled players and minimal log players
                // so we need to disconnect the "full" version of the client
                var client = GetClientsAsList().FirstOrDefault(_client => _client.Equals(E.Origin));

                if (client != null)
                {
#if DEBUG == true
                    Logger.WriteDebug($"Begin PreDisconnect for {client}");
#endif
                    await OnClientDisconnected(client);
#if DEBUG == true
                    Logger.WriteDebug($"End PreDisconnect for {client}");
#endif
                }

                else if (client?.State != ClientState.Disconnecting)
                {
                    Logger.WriteWarning($"Client {E.Origin} detected as disconnecting, but could not find them in the player list");
                    Logger.WriteDebug($"Expected {E.Origin} but found {GetClientsAsList().FirstOrDefault(_client => _client.ClientNumber == E.Origin.ClientNumber)}");
                    return false;
                }
            }

            else if (E.Type == GameEvent.EventType.Update)
            {
#if DEBUG == true
                Logger.WriteDebug($"Begin Update for {E.Origin}");
#endif
                await OnClientUpdate(E.Origin);
            }

            if (E.Type == GameEvent.EventType.Say)
            {
                E.Data = E.Data.StripColors();

                if (E.Data?.Length > 0)
                {
                    string message = E.Data;
                    if (E.Data.IsQuickMessage())
                    {
                        try
                        {
                            message = Manager.GetApplicationSettings().Configuration()
                                .QuickMessages
                                .First(_qm => _qm.Game == GameName)
                                .Messages[E.Data.Substring(1)];
                        }
                        catch { }
                    }

                    ChatHistory.Add(new ChatInfo()
                    {
                        Name = E.Origin.Name,
                        Message = message,
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

                    if (dict == null)
                    {
                        Logger.WriteWarning("Map change event response doesn't have any data");
                    }

                    else
                    {
                        Gametype = dict["gametype"].StripColors();
                        Hostname = dict["hostname"]?.StripColors();

                        string mapname = dict["mapname"]?.StripColors() ?? CurrentMap.Name;
                        CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map() { Alias = mapname, Name = mapname };
                    }
                }

                else
                {
                    var dict = (Dictionary<string, string>)E.Extra;
                    Gametype = dict["g_gametype"].StripColors();
                    Hostname = dict["sv_hostname"].StripColors();
                    MaxClients = int.Parse(dict["sv_maxclients"]);

                    string mapname = dict["mapname"].StripColors();
                    CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map()
                    {
                        Alias = mapname,
                        Name = mapname
                    };
                }
            }

            if (E.Type == GameEvent.EventType.MapEnd)
            {
                Logger.WriteInfo("Game ending...");
            }

            if (E.Type == GameEvent.EventType.Tell)
            {
                await Tell(E.Message, E.Target);
            }

            if (E.Type == GameEvent.EventType.Broadcast)
            {
#if DEBUG == false
                // this is a little ugly but I don't want to change the abstract class
                if (E.Data != null)
                {
                    await E.Owner.ExecuteCommandAsync(E.Data);
                }
#endif
            }

            lock (ChatHistory)
            {
                while (ChatHistory.Count > Math.Ceiling(ClientNum / 2.0))
                {
                    ChatHistory.RemoveAt(0);
                }
            }

            // the last client hasn't fully disconnected yet
            // so there will still be at least 1 client left
            if (ClientNum < 2)
            {
                ChatHistory.Clear();
            }

            return true;
        }

        private async Task OnClientUpdate(EFClient origin)
        {
            var client = GetClientsAsList().FirstOrDefault(_client => _client.Equals(origin));

            if (client != null)
            {
                client.Ping = origin.Ping;
                client.Score = origin.Score;

                // update their IP if it hasn't been set yet
                if (client.IPAddress == null &&
                    !client.IsBot &&
                    client.State == ClientState.Connected)
                {
                    try
                    {
                        await client.OnJoin(origin.IPAddress);
                    }

                    catch (Exception e)
                    {
                        origin.CurrentServer.Logger.WriteWarning($"Could not execute on join for {origin}");
                        origin.CurrentServer.Logger.WriteDebug(e.GetExceptionInfo());
                    }
                }
            }
        }

        /// <summary>
        /// lists the connecting and disconnecting clients via RCon response
        /// array index 0 =  connecting clients
        /// array index 1 = disconnecting clients
        /// </summary>
        /// <returns></returns>
        async Task<IList<EFClient>[]> PollPlayersAsync()
        {
#if DEBUG
            var now = DateTime.Now;
#endif
            var currentClients = GetClientsAsList();
            var polledClients = (await this.GetStatusAsync()).AsEnumerable();

            if (Manager.GetApplicationSettings().Configuration().IgnoreBots)
            {
                polledClients = polledClients.Where(c => !c.IsBot);
            }
#if DEBUG
            Logger.WriteInfo($"Polling players took {(DateTime.Now - now).TotalMilliseconds}ms");
#endif
            var disconnectingClients = currentClients.Except(polledClients);
            var connectingClients = polledClients.Except(currentClients);
            var updatedClients = polledClients.Except(connectingClients).Except(disconnectingClients);

            return new List<EFClient>[]
            {
                connectingClients.ToList(),
                disconnectingClients.ToList(),
                updatedClients.ToList()
            };
        }

        DateTime start = DateTime.Now;
        DateTime playerCountStart = DateTime.Now;
        DateTime lastCount = DateTime.Now;

        override public async Task<bool> ProcessUpdatesAsync(CancellationToken cts)
        {
            try
            {
                #region SHUTDOWN
                if (Manager.CancellationToken.IsCancellationRequested)
                {
                    foreach (var plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
                    {
                        await plugin.OnUnloadAsync();
                    }

                    return true;
                }
                #endregion

                try
                {
                    var polledClients = await PollPlayersAsync();
                    var waiterList = new List<GameEvent>();

                    foreach (var disconnectingClient in polledClients[1])
                    {
                        if (disconnectingClient.State == ClientState.Disconnecting)
                        {
                            continue;
                        }

                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.PreDisconnect,
                            Origin = disconnectingClient,
                            Owner = this
                        };

                        Manager.GetEventHandler().AddEvent(e);
                        // wait until the disconnect event is complete
                        // because we don't want to try to fill up a slot that's not empty yet
                        waiterList.Add(e);
                    }
                    // wait for all the disconnect tasks to finish
                    await Task.WhenAll(waiterList.Select(e => e.WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken)));

                    waiterList.Clear();
                    // this are our new connecting clients
                    foreach (var client in polledClients[0])
                    {
                        // note: this prevents players in ZMBI state from being registered with no name
                        if (string.IsNullOrEmpty(client.Name) || client.Ping == 999)
                        {
                            continue;
                        }

                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.PreConnect,
                            Origin = client,
                            Owner = this
                        };

                        Manager.GetEventHandler().AddEvent(e);
                        waiterList.Add(e);
                    }

                    // wait for all the connect tasks to finish
                    await Task.WhenAll(waiterList.Select(e => e.WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken)));

                    waiterList.Clear();
                    // these are the clients that have updated
                    foreach (var client in polledClients[2])
                    {
                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.Update,
                            Origin = client,
                            Owner = this
                        };

                        Manager.GetEventHandler().AddEvent(e);
                        waiterList.Add(e);
                    }

                    await Task.WhenAll(waiterList.Select(e => e.WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken)));

                    if (ConnectionErrors > 0)
                    {
                        var _event = new GameEvent()
                        {
                            Type = GameEvent.EventType.ConnectionRestored,
                            Owner = this,
                            Origin = Utilities.IW4MAdminClient(this),
                            Target = Utilities.IW4MAdminClient(this)
                        };

                        Manager.GetEventHandler().AddEvent(_event);
                    }

                    ConnectionErrors = 0;
                    LastPoll = DateTime.Now;
                }

                catch (NetworkException e)
                {
                    ConnectionErrors++;
                    if (ConnectionErrors == 3)
                    {
                        var _event = new GameEvent()
                        {
                            Type = GameEvent.EventType.ConnectionLost,
                            Owner = this,
                            Origin = Utilities.IW4MAdminClient(this),
                            Target = Utilities.IW4MAdminClient(this),
                            Extra = e,
                            Data = ConnectionErrors.ToString()
                        };

                        Manager.GetEventHandler().AddEvent(_event);
                    }
                    return true;
                }

                LastMessage = DateTime.Now - start;
                lastCount = DateTime.Now;

                // update the player history 
                if ((lastCount - playerCountStart).TotalMinutes >= PlayerHistory.UpdateInterval)
                {
                    while (ClientHistory.Count > ((60 / PlayerHistory.UpdateInterval) * 12)) // 12 times a hour for 12 hours
                    {
                        ClientHistory.Dequeue();
                    }

                    ClientHistory.Enqueue(new PlayerHistory(ClientNum));
                    playerCountStart = DateTime.Now;
                }

                // send out broadcast messages
                if (LastMessage.TotalSeconds > Manager.GetApplicationSettings().Configuration().AutoMessagePeriod
                    && BroadcastMessages.Count > 0
                    && ClientNum > 0)
                {
                    string[] messages = (await this.ProcessMessageToken(Manager.GetMessageTokens(), BroadcastMessages[NextMessage])).Split(Environment.NewLine);

                    foreach (string message in messages)
                    {
                        Broadcast(message);
                    }

                    NextMessage = NextMessage == (BroadcastMessages.Count - 1) ? 0 : NextMessage + 1;
                    start = DateTime.Now;
                }

                return true;
            }

            // this one is ok
            catch (ServerException e)
            {
                if (e is NetworkException && !Throttled)
                {
                    Logger.WriteError(loc["SERVER_ERROR_COMMUNICATION"].FormatExt($"{IP}:{Port}"));
                    Logger.WriteDebug(e.GetExceptionInfo());
                }

                return false;
            }

            catch (Exception E)
            {
                Logger.WriteError(loc["SERVER_ERROR_EXCEPTION"].FormatExt($"[{IP}:{Port}]"));
                Logger.WriteDebug(E.GetExceptionInfo());
                return false;
            }
        }

        public async Task Initialize()
        {
            RconParser = Manager.AdditionalRConParsers
                .FirstOrDefault(_parser => _parser.Version == ServerConfig.RConParserVersion);

            EventParser = Manager.AdditionalEventParsers
                .FirstOrDefault(_parser => _parser.Version == ServerConfig.EventParserVersion);

            RconParser = RconParser ?? new BaseRConParser();
            EventParser = EventParser ?? new BaseEventParser();

            RemoteConnection.SetConfiguration(RconParser.Configuration);

            var version = await this.GetDvarAsync<string>("version");
            Version = version.Value;
            GameName = Utilities.GetGame(version?.Value ?? RconParser.Version);

            if (GameName == Game.UKN)
            {
                GameName = RconParser.GameName;
            }

            if (version?.Value?.Length != 0)
            {
                RconParser = Manager.AdditionalRConParsers.FirstOrDefault(_parser => _parser.Version == version.Value) ?? RconParser;
                EventParser = Manager.AdditionalEventParsers.FirstOrDefault(_parser => _parser.Version == version.Value) ?? EventParser;
                Version = RconParser.Version;
            }

            var infoResponse = RconParser.Configuration.CommandPrefixes.RConGetInfo != null ? await this.GetInfoAsync() : null;
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
            var ip = await this.GetDvarAsync<string>("net_ip");

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
            this.IP = ip.Value == "localhost" ? ServerConfig.IPAddress : ip.Value ?? ServerConfig.IPAddress;

            if ((logsync.Value == 0 || logfile.Value == string.Empty) && RconParser.CanGenerateLogPath)
            {
                // this DVAR isn't set until the a map is loaded
                await this.SetDvarAsync("logfile", 2);
                await this.SetDvarAsync("g_logsync", 2); // set to 2 for continous in other games, clamps to 1 for IW4
                                                         //await this.SetDvarAsync("g_log", "games_mp.log");
                Logger.WriteWarning("Game log file not properly initialized, restarting map...");
                await this.ExecuteCommandAsync("map_restart");
                logfile = await this.GetDvarAsync<string>("g_log");
            }

            CustomCallback = await ScriptLoaded();

            // they've manually specified the log path
            if (!string.IsNullOrEmpty(ServerConfig.ManualLogPath))
            {
                LogPath = ServerConfig.ManualLogPath;
            }

            else
            {
                string mainPath = EventParser.Configuration.GameDirectory;

                LogPath = string.IsNullOrEmpty(game) ?
                       $"{basepath?.Value?.Replace('\\', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{mainPath}{Path.DirectorySeparatorChar}{logfile?.Value}" :
                       $"{basepath?.Value?.Replace('\\', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{game?.Replace('/', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{logfile?.Value}";

                // fix wine drive name mangling
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    LogPath = Regex.Replace($"{Path.DirectorySeparatorChar}{LogPath}", @"[A-Z]:/", "");
                }

                if (!File.Exists(LogPath) && ServerConfig.GameLogServerUrl == null)
                {
                    Logger.WriteError(loc["SERVER_ERROR_DNE"].FormatExt(LogPath));
                    throw new ServerException(loc["SERVER_ERROR_DNE"].FormatExt(LogPath));
                }
            }

            LogEvent = new GameLogEventDetection(this, LogPath, ServerConfig.GameLogServerUrl);
            Logger.WriteInfo($"Log file is {LogPath}");

            _ = Task.Run(() => LogEvent.PollForChanges());
#if !DEBUG
            Broadcast(loc["BROADCAST_ONLINE"]);
#endif
        }

        protected override async Task Warn(String Reason, EFClient Target, EFClient Origin)
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
                    Target.Kick(loc["SERVER_WARNLIMT_REACHED"], Utilities.IW4MAdminClient(this));
                    return;
                }

                string message = $"^1{loc["SERVER_WARNING"]} ^7[^3{Target.Warnings}^7]: ^3{Target.Name}^7, {Reason}";
                Target.CurrentServer.Broadcast(message);
            }

            var newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Warning,
                Expires = DateTime.UtcNow,
                Offender = Target,
                Punisher = Origin,
                Offense = Reason,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        protected override async Task Kick(String Reason, EFClient Target, EFClient Origin)
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
                string formattedKick = string.Format(RconParser.Configuration.CommandPrefixes.Kick, Target.ClientNumber, $"{loc["SERVER_KICK_TEXT"]} - ^5{Reason}^7");
                await Target.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
#endif

#if DEBUG
            await Target.CurrentServer.OnClientDisconnected(Target);
#endif

            var newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Kick,
                Expires = DateTime.UtcNow,
                Offender = Target,
                Offense = Reason,
                Punisher = Origin,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        protected override async Task TempBan(String Reason, TimeSpan length, EFClient Target, EFClient Origin)
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
                string formattedKick = String.Format(RconParser.Configuration.CommandPrefixes.Kick, Target.ClientNumber, $"^7{loc["SERVER_TB_TEXT"]}- ^5{Reason}");
                await Target.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
#else
            await Target.CurrentServer.OnClientDisconnected(Target);
#endif

            var newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.TempBan,
                Expires = DateTime.UtcNow + length,
                Offender = Target,
                Offense = Reason,
                Punisher = Origin,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().Create(newPenalty);
        }

        override protected async Task Ban(string reason, EFClient targetClient, EFClient originClient, bool isEvade = false)
        {
            // ensure player gets banned if command not performed on them in game
            if (targetClient.ClientNumber < 0)
            {
                EFClient ingameClient = null;

                ingameClient = Manager.GetServers()
                    .Select(s => s.GetClientsAsList())
                    .FirstOrDefault(l => l.FirstOrDefault(c => c.ClientId == targetClient?.ClientId) != null)
                    ?.First(c => c.ClientId == targetClient.ClientId);

                if (ingameClient != null)
                {
                    await Ban(reason, ingameClient, originClient, isEvade);
                    return;
                }
            }

            else
            {
#if !DEBUG
                string formattedString = string.Format(RconParser.Configuration.CommandPrefixes.Kick, targetClient.ClientNumber, $"{loc["SERVER_BAN_TEXT"]} - ^5{reason} ^7{loc["SERVER_BAN_APPEAL"].FormatExt(Website)}^7");
                await targetClient.CurrentServer.ExecuteCommandAsync(formattedString);
#else
                await targetClient.CurrentServer.OnClientDisconnected(targetClient);
#endif
            }

            // this should link evading clients
            if (isEvade)
            {
                Logger.WriteInfo($"updating alias for banned client {targetClient}");
                await Manager.GetClientService().UpdateAlias(targetClient);
            }

            EFPenalty newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Ban,
                Expires = null,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                Link = targetClient.AliasLink,
                AutomatedOffense = originClient.AdministeredPenalties?.FirstOrDefault()?.AutomatedOffense,
                IsEvadedOffense = isEvade
            };

            targetClient.SetLevel(Permission.Banned, originClient);
            await Manager.GetPenaltyService().Create(newPenalty);
        }

        override public async Task Unban(string reason, EFClient Target, EFClient Origin)
        {
            var unbanPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Unban,
                Expires = null,
                Offender = Target,
                Offense = reason,
                Punisher = Origin,
                When = DateTime.UtcNow,
                Active = true,
                Link = Target.AliasLink
            };

            await Manager.GetPenaltyService().RemoveActivePenalties(Target.AliasLink.AliasLinkId, Origin);
            await Manager.GetPenaltyService().Create(unbanPenalty);
            Target.SetLevel(Permission.User, Origin);
        }

        override public void InitializeTokens()
        {
            Manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYERS", (Server s) => Task.Run(async () => (await Manager.GetClientService().GetTotalClientsAsync()).ToString())));
            Manager.GetMessageTokens().Add(new MessageToken("VERSION", (Server s) => Task.FromResult(Application.Program.Version.ToString())));
            Manager.GetMessageTokens().Add(new MessageToken("NEXTMAP", (Server s) => SharedLibraryCore.Commands.CNextMap.GetNextMap(s)));
            Manager.GetMessageTokens().Add(new MessageToken("ADMINS", (Server s) => Task.FromResult(SharedLibraryCore.Commands.CListAdmins.OnlineAdmins(s))));
        }
    }
}
