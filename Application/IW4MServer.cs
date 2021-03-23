using IW4MAdmin.Application.IO;
using IW4MAdmin.Application.Misc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using static SharedLibraryCore.Database.Models.EFClient;
using Data.Models;
using Data.Models.Server;
using static Data.Models.Client.EFClient;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        private static readonly SharedLibraryCore.Localization.TranslationLookup loc = Utilities.CurrentLocalization.LocalizationIndex;
        public GameLogEventDetection LogEvent;
        private readonly ITranslationLookup _translationLookup;
        private readonly IMetaService _metaService;
        private const int REPORT_FLAG_COUNT = 4;
        private long lastGameTime = 0;

        public int Id { get; private set; }
        private readonly IServiceProvider _serviceProvider;
        private readonly IClientNoticeMessageFormatter _messageFormatter;
        private readonly ILookupCache<EFServer> _serverCache;

        public IW4MServer(
            ServerConfiguration serverConfiguration,
            ITranslationLookup lookup,
            IMetaService metaService, 
            IServiceProvider serviceProvider,
            IClientNoticeMessageFormatter messageFormatter,
            ILookupCache<EFServer> serverCache) : base(serviceProvider.GetRequiredService<ILogger<Server>>(), 
            serviceProvider.GetRequiredService<SharedLibraryCore.Interfaces.ILogger>(), 
            serverConfiguration,
            serviceProvider.GetRequiredService<IManager>(), 
            serviceProvider.GetRequiredService<IRConConnectionFactory>(),
            serviceProvider.GetRequiredService<IGameLogReaderFactory>())
        {
            _translationLookup = lookup;
            _metaService = metaService;
            _serviceProvider = serviceProvider;
            _messageFormatter = messageFormatter;
            _serverCache = serverCache;
        }

        public override async Task<EFClient> OnClientConnected(EFClient clientFromLog)
        {
            ServerLogger.LogDebug("Client slot #{clientNumber} now reserved", clientFromLog.ClientNumber);

            EFClient client = await Manager.GetClientService().GetUnique(clientFromLog.NetworkId);

            // first time client is connecting to server
            if (client == null)
            {
                ServerLogger.LogDebug("Client {client} first time connecting", clientFromLog.ToString());
                clientFromLog.CurrentServer = this;
                client = await Manager.GetClientService().Create(clientFromLog);
            }

            // this is only a temporary version until the IPAddress is transmitted
            client.CurrentAlias = new EFAlias()
            {
                Name = clientFromLog.Name,
                IPAddress = clientFromLog.IPAddress
            };

            // Do the player specific stuff
            client.ClientNumber = clientFromLog.ClientNumber;
            client.Score = clientFromLog.Score;
            client.Ping = clientFromLog.Ping;
            client.CurrentServer = this;
            client.State = ClientState.Connecting;

            Clients[client.ClientNumber] = client;
            ServerLogger.LogDebug("End PreConnect for {client}", client.ToString());
            var e = new GameEvent()
            {
                Origin = client,
                Owner = this,
                Type = GameEvent.EventType.Connect
            };

            Manager.AddEvent(e);
            return client;
        }

        public override async Task OnClientDisconnected(EFClient client)
        {
            if (!GetClientsAsList().Any(_client => _client.NetworkId == client.NetworkId))
            {
                using (LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogWarning("{client} disconnecting, but they are not connected", client.ToString());
                }
                return;
            }

#if DEBUG == true
            if (client.ClientNumber >= 0)
            {
#endif
                ServerLogger.LogDebug("Client {@client} disconnecting...", new { client=client.ToString(), client.State });
                Clients[client.ClientNumber] = null;
                await client.OnDisconnect();

                var e = new GameEvent()
                {
                    Origin = client,
                    Owner = this,
                    Type = GameEvent.EventType.Disconnect
                };

                Manager.AddEvent(e);
#if DEBUG == true
            }
#endif
        }

        public override async Task ExecuteEvent(GameEvent E)
        {
            using (LogContext.PushProperty("Server", ToString()))
            {
                if (E.IsBlocking)
                {
                    await E.Origin?.Lock();
                }

                bool canExecuteCommand = true;

                try
                {
                    if (!await ProcessEvent(E))
                    {
                        return;
                    }

                    Command C = null;
                    if (E.Type == GameEvent.EventType.Command)
                    {
                        try
                        {
                            C = await SharedLibraryCore.Commands.CommandProcessing.ValidateCommand(E, Manager.GetApplicationSettings().Configuration());
                        }

                        catch (CommandException e)
                        {
                            ServerLogger.LogWarning(e, "Error validating command from event {@event}", 
                                new { E.Type, E.Data, E.Message, E.Subtype, E.IsRemote, E.CorrelationId });
                            E.FailReason = GameEvent.EventFailReason.Invalid;
                        }

                        if (C != null)
                        {
                            E.Extra = C;
                        }
                    }

                    try
                    {
                        var loginPlugin = Manager.Plugins.FirstOrDefault(_plugin => _plugin.Name == "Login");

                        if (loginPlugin != null)
                        {
                            await loginPlugin.OnEventAsync(E, this);
                        }
                    }

                    catch (AuthorizationException e)
                    {
                        E.Origin.Tell($"{loc["COMMAND_NOTAUTHORIZED"]} - {e.Message}");
                        canExecuteCommand = false;
                    }

                    // hack: this prevents commands from getting executing that 'shouldn't' be
                    if (E.Type == GameEvent.EventType.Command && E.Extra is Command command &&
                        (canExecuteCommand || E.Origin?.Level == Permission.Console))
                    {
                        ServerLogger.LogInformation("Executing command {comamnd} for {client}", command.Name, E.Origin.ToString());
                        await command.ExecuteAsync(E);
                    }

                    var pluginTasks = Manager.Plugins
                        .Where(_plugin => _plugin.Name != "Login")
                        .Select(async plugin => await CreatePluginTask(plugin, E));
                    
                    await Task.WhenAll(pluginTasks);
                }

                catch (Exception e)
                {
                    ServerLogger.LogError(e, "Unexpected exception occurred processing event");
                    if (E.Origin != null && E.Type == GameEvent.EventType.Command)
                    {
                        E.Origin.Tell(_translationLookup["SERVER_ERROR_COMMAND_INGAME"]);
                    }
                }

                finally
                {
                    if (E.IsBlocking)
                    {
                        E.Origin?.Unlock();
                    }
                }
            }
        }

        private async Task CreatePluginTask(IPlugin plugin, GameEvent gameEvent)
        {
            // we don't want to run the events on parser plugins
            if (plugin is ScriptPlugin scriptPlugin && scriptPlugin.IsParser)
            {
                return;
            }

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(Utilities.DefaultCommandTimeout);

            try
            {
                await (plugin.OnEventAsync(gameEvent, this)).WithWaitCancellation(tokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(loc["SERVER_PLUGIN_ERROR"]);
                ServerLogger.LogError(ex, "Could not execute {methodName} for plugin {plugin}", 
                    nameof(plugin.OnEventAsync), plugin.Name);
            }
        }

        /// <summary>
        /// Perform the server specific tasks when an event occurs 
        /// </summary>
        /// <param name="E"></param>
        /// <returns></returns>
        protected override async Task<bool> ProcessEvent(GameEvent E)
        {
            using (LogContext.PushProperty("Server", ToString()))
                using (LogContext.PushProperty("EventType", E.Type))
            {
                ServerLogger.LogDebug("processing event of type {type}", E.Type);

                if (E.Type == GameEvent.EventType.Start)
                {
                    var existingServer = (await _serverCache
                        .FirstAsync(server => server.Id == EndPoint));

                    var serverId = await GetIdForServer(E.Owner);

                    if (existingServer == null)
                    {
                        var server = new EFServer()
                        {
                            Port = Port,
                            EndPoint = ToString(),
                            ServerId = serverId,
                            GameName = (Reference.Game?)GameName,
                            HostName = Hostname
                        };

                        await _serverCache.AddAsync(server);
                    }
                }
                
                if (E.Type == GameEvent.EventType.ConnectionLost)
                {
                    var exception = E.Extra as Exception;
                    ServerLogger.LogError(exception,
                        "Connection lost with {server}", ToString());

                    if (!Manager.GetApplicationSettings().Configuration().IgnoreServerConnectionLost)
                    {
                        Console.WriteLine(loc["SERVER_ERROR_COMMUNICATION"].FormatExt($"{IP}:{Port}"));
                    }

                    Throttled = true;
                }

                if (E.Type == GameEvent.EventType.ConnectionRestored)
                {
                    ServerLogger.LogInformation(
                        "Connection restored with {server}", ToString());
                    
                    if (!Manager.GetApplicationSettings().Configuration().IgnoreServerConnectionLost)
                    {
                        Console.WriteLine(loc["MANAGER_CONNECTION_REST"].FormatExt($"[{IP}:{Port}]"));
                    }

                    Throttled = false;
                }

                if (E.Type == GameEvent.EventType.ChangePermission)
                {
                    var newPermission = (Permission) E.Extra;
                    ServerLogger.LogInformation("{origin} is setting {target} to permission level {newPermission}",
                        E.Origin.ToString(), E.Target.ToString(), newPermission);
                    await Manager.GetClientService().UpdateLevel(newPermission, E.Target, E.Origin);
                }

                else if (E.Type == GameEvent.EventType.Connect)
                {
                    if (E.Origin.State != ClientState.Connected)
                    {
                        E.Origin.State = ClientState.Connected;
                        E.Origin.LastConnection = DateTime.UtcNow;
                        E.Origin.Connections += 1;

                        ChatHistory.Add(new ChatInfo()
                        {
                            Name = E.Origin.Name,
                            Message = "CONNECTED",
                            Time = DateTime.UtcNow
                        });

                        var clientTag = await _metaService.GetPersistentMeta(EFMeta.ClientTag, E.Origin);

                        if (clientTag?.LinkedMeta != null)
                        {
                            E.Origin.Tag = clientTag.LinkedMeta.Value;
                        }

                        await E.Origin.OnJoin(E.Origin.IPAddress);
                    }
                }

                else if (E.Type == GameEvent.EventType.PreConnect)
                {
                    ServerLogger.LogInformation("Detected PreConnect for {client} from {source}", E.Origin.ToString(), E.Source);
                    // we don't want to track bots in the database at all if ignore bots is requested
                    if (E.Origin.IsBot && Manager.GetApplicationSettings().Configuration().IgnoreBots)
                    {
                        return false;
                    }

                    if (E.Origin.CurrentServer == null)
                    {
                        ServerLogger.LogWarning("Preconnecting client {client} did not have a current server specified",
                            E.Origin.ToString());
                        E.Origin.CurrentServer = this;
                    }

                    var existingClient = GetClientsAsList().FirstOrDefault(_client => _client.Equals(E.Origin));

                    // they're already connected
                    if (existingClient != null && existingClient.ClientNumber == E.Origin.ClientNumber &&
                        !E.Origin.IsBot)
                    {
                        ServerLogger.LogInformation("{client} is already connected, so we are ignoring their PreConnect",
                            E.Origin.ToString());
                        return false;
                    }

                    // this happens for some reason rarely where the client spots get out of order
                    // possible a connect/reconnect game event before we get to process it here 
                    // it appears that new games decide to switch client slots between maps (even if the clients aren't disconnecting)
                    // bots can have duplicate names which causes conflicting GUIDs
                    if (existingClient != null && existingClient.ClientNumber != E.Origin.ClientNumber &&
                             !E.Origin.IsBot)
                    {
                        ServerLogger.LogWarning(
                            "client {client} is trying to connect in client slot {newClientSlot}, but they are already registered in client slot {oldClientSlot}, swapping...",
                            E.Origin.ToString(), E.Origin.ClientNumber, existingClient.ClientNumber);
                        // we need to remove them so the client spots can swap
                        await OnClientDisconnected(Clients[existingClient.ClientNumber]);
                    }

                    if (Clients[E.Origin.ClientNumber] == null)
                    {
                        ServerLogger.LogDebug("Begin PreConnect for {origin}", E.Origin.ToString());
                        // we can go ahead and put them in so that they don't get re added
                        Clients[E.Origin.ClientNumber] = E.Origin;
                        try
                        {
                            E.Origin = await OnClientConnected(E.Origin);
                            E.Target = E.Origin;
                        }

                        catch (Exception ex)
                        {
                            Console.WriteLine($"{loc["SERVER_ERROR_ADDPLAYER"]} {E.Origin}");
                            ServerLogger.LogError(ex, "Could not add player {player}", E.Origin.ToString());
                            Clients[E.Origin.ClientNumber] = null;
                            return false;
                        }

                        if (E.Origin.Level > Permission.Moderator)
                        {
                            E.Origin.Tell(string.Format(loc["SERVER_REPORT_COUNT"], E.Owner.Reports.Count));
                        }
                    }

                    // for some reason there's still a client in the spot
                    else
                    {
                        ServerLogger.LogWarning(
                            "{origin} is connecting but {existingClient} is currently in that client slot",
                            E.Origin.ToString(), Clients[E.Origin.ClientNumber].ToString());
                    }
                }

                else if (E.Type == GameEvent.EventType.Flag)
                {
                    DateTime? expires = null;

                    if (E.Extra is TimeSpan ts)
                    {
                        expires = DateTime.UtcNow + ts;
                    }

                    // todo: maybe move this to a seperate function
                    var newPenalty = new EFPenalty()
                    {
                        Type = EFPenalty.PenaltyType.Flag,
                        Expires = expires,
                        Offender = E.Target,
                        Offense = E.Data,
                        Punisher = E.ImpersonationOrigin ?? E.Origin,
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
                        Punisher = E.ImpersonationOrigin ?? E.Origin,
                        When = DateTime.UtcNow,
                        Link = E.Target.AliasLink
                    };

                    E.Target.SetLevel(Permission.User, E.Origin);
                    await Manager.GetPenaltyService().RemoveActivePenalties(E.Target.AliasLinkId);
                    await Manager.GetPenaltyService().Create(unflagPenalty);
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
                        Punisher = E.ImpersonationOrigin ?? E.Origin,
                        Active = true,
                        When = DateTime.UtcNow,
                        Link = E.Target.AliasLink
                    };

                    await Manager.GetPenaltyService().Create(newReport);

                    var reportNum = await Manager.GetClientService().GetClientReportCount(E.Target.ClientId);
                    var canBeAutoFlagged = await Manager.GetClientService().CanBeAutoFlagged(E.Target.ClientId);

                    if (!E.Target.IsPrivileged() && reportNum >= REPORT_FLAG_COUNT && canBeAutoFlagged)
                    {
                        E.Target.Flag(
                            Utilities.CurrentLocalization.LocalizationIndex["SERVER_AUTO_FLAG_REPORT"]
                                .FormatExt(reportNum), Utilities.IW4MAdminClient(E.Owner));
                    }
                }

                else if (E.Type == GameEvent.EventType.TempBan)
                {
                    await TempBan(E.Data, (TimeSpan) E.Extra, E.Target, E.ImpersonationOrigin ?? E.Origin);
                }

                else if (E.Type == GameEvent.EventType.Ban)
                {
                    bool isEvade = E.Extra != null ? (bool) E.Extra : false;
                    await Ban(E.Data, E.Target, E.ImpersonationOrigin ?? E.Origin, isEvade);
                }

                else if (E.Type == GameEvent.EventType.Unban)
                {
                    await Unban(E.Data, E.Target, E.ImpersonationOrigin ?? E.Origin);
                }

                else if (E.Type == GameEvent.EventType.Kick)
                {
                    await Kick(E.Data, E.Target, E.ImpersonationOrigin ?? E.Origin, E.Extra as EFPenalty);
                }

                else if (E.Type == GameEvent.EventType.Warn)
                {
                    await Warn(E.Data, E.Target, E.ImpersonationOrigin ?? E.Origin);
                }

                else if (E.Type == GameEvent.EventType.Disconnect)
                {
                    ChatHistory.Add(new ChatInfo()
                    {
                        Name = E.Origin.Name,
                        Message = "DISCONNECTED",
                        Time = DateTime.UtcNow
                    });

                    await _metaService.AddPersistentMeta("LastMapPlayed", CurrentMap.Alias, E.Origin);
                    await _metaService.AddPersistentMeta("LastServerPlayed", E.Owner.Hostname, E.Origin);
                }

                else if (E.Type == GameEvent.EventType.PreDisconnect)
                {
                    ServerLogger.LogInformation("Detected PreDisconnect for {client} from {source}", 
                        E.Origin.ToString(), E.Source);
                    bool isPotentialFalseQuit = E.GameTime.HasValue && E.GameTime.Value == lastGameTime;

                    if (isPotentialFalseQuit)
                    {
                        ServerLogger.LogDebug(
                            "Received PreDisconnect event for {origin}, but it occured at game time {gameTime}, which is the same last map change, so we're ignoring",
                            E.Origin.ToString(), E.GameTime);
                        return false;
                    }

                    // predisconnect comes from minimal rcon polled players and minimal log players
                    // so we need to disconnect the "full" version of the client
                    var client = GetClientsAsList().FirstOrDefault(_client => _client.Equals(E.Origin));

                    if (client == null)
                    {
                        // this can happen when the status picks up the connect before the log does
                        ServerLogger.LogInformation(
                            "Ignoring PreDisconnect for {origin} because they are no longer on the client list",
                            E.Origin.ToString());
                        return false;
                    }

                    else if (client.State != ClientState.Unknown)
                    {
                        await OnClientDisconnected(client);
                        return true;
                    }

                    else
                    {
                        ServerLogger.LogWarning(
                            "Expected disconnecting client {client} to be in state {state}, but is in state {clientState}",
                            client.ToString(), ClientState.Connected.ToString(), client.State);
                        return false;
                    }
                }

                else if (E.Type == GameEvent.EventType.Update)
                {
                    ServerLogger.LogDebug("Begin Update for {origin}", E.Origin.ToString());
                    await OnClientUpdate(E.Origin);
                }

                if (E.Type == GameEvent.EventType.Say)
                {
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
                            catch
                            {
                                message = E.Data.Substring(1);
                            }
                        }

                        ChatHistory.Add(new ChatInfo()
                        {
                            Name = E.Origin.Name,
                            Message = message,
                            Time = DateTime.UtcNow,
                            IsHidden = !string.IsNullOrEmpty(GamePassword)
                        });
                    }
                }

                if (E.Type == GameEvent.EventType.MapChange)
                {
                    ServerLogger.LogInformation("New map loaded - {clientCount} active players", ClientNum);

                    // iw4 doesn't log the game info
                    if (E.Extra == null)
                    {
                        var dict = await this.GetInfoAsync(new TimeSpan(0, 0, 20));

                        if (dict == null)
                        {
                            ServerLogger.LogWarning("Map change event response doesn't have any data");
                        }

                        else
                        {
                            Gametype = dict["gametype"];
                            Hostname = dict["hostname"];

                            string mapname = dict["mapname"] ?? CurrentMap.Name;
                            UpdateMap(mapname);
                        }
                    }

                    else
                    {
                        var dict = (Dictionary<string, string>) E.Extra;
                        Gametype = dict["g_gametype"];
                        Hostname = dict["sv_hostname"];
                        MaxClients = int.Parse(dict["sv_maxclients"]);

                        string mapname = dict["mapname"];
                        UpdateMap(mapname);
                    }

                    if (E.GameTime.HasValue)
                    {
                        lastGameTime = E.GameTime.Value;
                    }
                }

                if (E.Type == GameEvent.EventType.MapEnd)
                {
                    ServerLogger.LogInformation("Game ending...");

                    if (E.GameTime.HasValue)
                    {
                        lastGameTime = E.GameTime.Value;
                    }
                }

                if (E.Type == GameEvent.EventType.Tell)
                {
                    await Tell(E.Message, E.Target);
                }

                if (E.Type == GameEvent.EventType.Broadcast)
                {
                    if (!Utilities.IsDevelopment && E.Data != null) // hides broadcast when in development mode
                    {
                        await E.Owner.ExecuteCommandAsync(E.Data);
                    }
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
        }

        private async Task OnClientUpdate(EFClient origin)
        {
            var client = GetClientsAsList().FirstOrDefault(_client => _client.Equals(origin));

            if (client == null)
            {
                ServerLogger.LogWarning("{origin} expected to exist in client list for update, but they do not", origin.ToString());
                return;
            }

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
                    using(LogContext.PushProperty("Server", ToString()))
                    {
                        ServerLogger.LogError(e, "Could not execute on join for {origin}", origin.ToString());
                    }
                }
            }

            else if ((client.IPAddress != null && client.State == ClientState.Disconnecting) ||
                client.Level == Permission.Banned)
            {
                ServerLogger.LogWarning("{client} state is Unknown (probably kicked), but they are still connected. trying to kick again...", origin.ToString());
                await client.CanConnect(client.IPAddress);
            }
        }

        /// <summary>
        /// lists the connecting and disconnecting clients via RCon response
        /// array index 0 = connecting clients
        /// array index 1 = disconnecting clients
        /// array index 2 = updated clients
        /// </summary>
        /// <returns></returns>
        async Task<IList<EFClient>[]> PollPlayersAsync()
        {
            var currentClients = GetClientsAsList();
            var statusResponse = (await this.GetStatusAsync());
            var polledClients = statusResponse.Item1.AsEnumerable();

            if (Manager.GetApplicationSettings().Configuration().IgnoreBots)
            {
                polledClients = polledClients.Where(c => !c.IsBot);
            }
            var disconnectingClients = currentClients.Except(polledClients);
            var connectingClients = polledClients.Except(currentClients);
            var updatedClients = polledClients.Except(connectingClients).Except(disconnectingClients);

            UpdateMap(statusResponse.Item2);
            UpdateGametype(statusResponse.Item3);

            return new List<EFClient>[]
            {
                connectingClients.ToList(),
                disconnectingClients.ToList(),
                updatedClients.ToList()
            };
        }
        
        private async Task<long> GetIdForServer(Server server)
        {
            if ($"{server.IP}:{server.Port.ToString()}" == "66.150.121.184:28965")
            {
                return 886229536;
            }

            // todo: this is not stable and will need to be migrated again...
            long id = HashCode.Combine(server.IP, server.Port);
            id = id < 0 ? Math.Abs(id) : id;

            var serverId = (await _serverCache
                    .FirstAsync(_server => _server.ServerId == server.EndPoint ||
                                                                    _server.EndPoint == server.ToString() ||
                                                                    _server.ServerId == id))?.ServerId;

            return !serverId.HasValue ? id : serverId.Value;
        }

        private void UpdateMap(string mapname)
        {
            if (!string.IsNullOrEmpty(mapname))
            {
                CurrentMap = Maps.Find(m => m.Name == mapname) ?? new Map()
                {
                    Alias = mapname,
                    Name = mapname
                };
            }
        }

        private void UpdateGametype(string gameType)
        {
            if (!string.IsNullOrEmpty(gameType))
            {
                Gametype = gameType;
            }
        }

        private async Task ShutdownInternal()
        {
            foreach (var client in GetClientsAsList())
            {
                await client.OnDisconnect();

                var e = new GameEvent()
                {
                    Type = GameEvent.EventType.Disconnect,
                    Owner = this,
                    Origin = client
                };

                Manager.AddEvent(e);

                await e.WaitAsync(Utilities.DefaultCommandTimeout, new CancellationTokenRegistration().Token);
            }

            foreach (var plugin in Manager.Plugins)
            {
                await plugin.OnUnloadAsync();
            }
        }

        DateTime start = DateTime.Now;
        DateTime playerCountStart = DateTime.Now;
        DateTime lastCount = DateTime.Now;

        public override async Task<bool> ProcessUpdatesAsync(CancellationToken cts)
        {
            try
            {
                if (cts.IsCancellationRequested)
                {
                    await ShutdownInternal();
                    return true;
                }

                try
                {
                    if (Manager.GetApplicationSettings().Configuration().RConPollRate == int.MaxValue && Utilities.IsDevelopment)
                    {
                        return true;
                    }

                    var polledClients = await PollPlayersAsync();

                    foreach (var disconnectingClient in polledClients[1].Where(_client => !_client.IsZombieClient /* ignores "fake" zombie clients */))
                    {
                        disconnectingClient.CurrentServer = this;
                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.PreDisconnect,
                            Origin = disconnectingClient,
                            Owner = this,
                            Source = GameEvent.EventSource.Status
                        };

                        Manager.AddEvent(e);
                        await e.WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken);
                    }

                    // this are our new connecting clients
                    foreach (var client in polledClients[0])
                    {
                        // note: this prevents players in ZMBI state from being registered with no name
                        if (string.IsNullOrEmpty(client.Name) || (client.Ping == 999 && !client.IsBot))
                        {
                            continue;
                        }

                        client.CurrentServer = this;
                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.PreConnect,
                            Origin = client,
                            Owner = this,
                            IsBlocking = true,
                            Extra = client.GetAdditionalProperty<string>("BotGuid"),
                            Source = GameEvent.EventSource.Status
                        };

                        Manager.AddEvent(e);
                        await e.WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken);
                    }

                    // these are the clients that have updated
                    foreach (var client in polledClients[2])
                    {
                        client.CurrentServer = this;
                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.Update,
                            Origin = client,
                            Owner = this
                        };

                        Manager.AddEvent(e);
                    }

                    if (Throttled)
                    {
                        var _event = new GameEvent()
                        {
                            Type = GameEvent.EventType.ConnectionRestored,
                            Owner = this,
                            Origin = Utilities.IW4MAdminClient(this),
                            Target = Utilities.IW4MAdminClient(this)
                        };

                        Manager.AddEvent(_event);
                    }

                    LastPoll = DateTime.Now;
                }

                catch (NetworkException e)
                {
                    if (!Throttled)
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

                        Manager.AddEvent(_event);
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

            catch (TaskCanceledException)
            {
                await ShutdownInternal();
                return true;
            }

            // this one is ok
            catch (Exception e) when(e is ServerException || e is RConException)
            {
                using(LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogWarning(e, "Undesirable exception occured during processing updates");
                }
                return false;
            }

            catch (Exception e)
            {
                using(LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogError(e, "Unexpected exception occured during processing updates");
                }
                Console.WriteLine(loc["SERVER_ERROR_EXCEPTION"].FormatExt($"[{IP}:{Port}]"));
                return false;
            }
        }

        public async Task Initialize()
        {
            RconParser = Manager.AdditionalRConParsers
                .FirstOrDefault(_parser => _parser.Version == ServerConfig.RConParserVersion);

            EventParser = Manager.AdditionalEventParsers
                .FirstOrDefault(_parser => _parser.Version == ServerConfig.EventParserVersion);

            RconParser = RconParser ?? Manager.AdditionalRConParsers[0];
            EventParser = EventParser ?? Manager.AdditionalEventParsers[0];

            RemoteConnection.SetConfiguration(RconParser);

            var version = await this.GetMappedDvarValueOrDefaultAsync<string>("version");
            Version = version.Value;
            GameName = Utilities.GetGame(version?.Value ?? RconParser.Version);

            if (GameName == Game.UKN)
            {
                GameName = RconParser.GameName;
            }

            if (version?.Value?.Length != 0)
            {
                var matchedRconParser = Manager.AdditionalRConParsers.FirstOrDefault(_parser => _parser.Version == version.Value);
                RconParser.Configuration = matchedRconParser != null ? matchedRconParser.Configuration : RconParser.Configuration;
                EventParser = Manager.AdditionalEventParsers.FirstOrDefault(_parser => _parser.Version == version.Value) ?? EventParser;
                Version = RconParser.Version;
            }

            var svRunning = await this.GetMappedDvarValueOrDefaultAsync<string>("sv_running");

            if (!string.IsNullOrEmpty(svRunning.Value) && svRunning.Value != "1")
            {
                throw new ServerException(loc["SERVER_ERROR_NOT_RUNNING"].FormatExt(this.ToString()));
            }

            var infoResponse = RconParser.Configuration.CommandPrefixes.RConGetInfo != null ? await this.GetInfoAsync() : null;

            string hostname = (await this.GetMappedDvarValueOrDefaultAsync<string>("sv_hostname", "hostname", infoResponse)).Value;
            string mapname = (await this.GetMappedDvarValueOrDefaultAsync<string>("mapname", infoResponse: infoResponse)).Value;
            int maxplayers = (await this.GetMappedDvarValueOrDefaultAsync<int>("sv_maxclients", infoResponse: infoResponse)).Value;
            string gametype = (await this.GetMappedDvarValueOrDefaultAsync<string>("g_gametype", "gametype", infoResponse)).Value;
            var basepath = (await this.GetMappedDvarValueOrDefaultAsync<string>("fs_basepath"));
            var basegame = (await this.GetMappedDvarValueOrDefaultAsync<string>("fs_basegame"));
            var game = (await this.GetMappedDvarValueOrDefaultAsync<string>("fs_game", infoResponse: infoResponse));
            var logfile = await this.GetMappedDvarValueOrDefaultAsync<string>("g_log");
            var logsync = await this.GetMappedDvarValueOrDefaultAsync<int>("g_logsync");
            var ip = await this.GetMappedDvarValueOrDefaultAsync<string>("net_ip");
            var gamePassword = await this.GetMappedDvarValueOrDefaultAsync("g_password", overrideDefault: "");

            if (Manager.GetApplicationSettings().Configuration().EnableCustomSayName)
            {
                await this.SetDvarAsync("sv_sayname", Manager.GetApplicationSettings().Configuration().CustomSayName);
            }

            try
            {
                var website = await this.GetMappedDvarValueOrDefaultAsync<string>("_website");

                // this occurs for games that don't give us anything back when
                // the dvar is not set
                if (string.IsNullOrWhiteSpace(website.Value))
                {
                    throw new DvarException("value is empty");
                }

                Website = website.Value;
            }

            catch (DvarException)
            {
                Website = loc["SERVER_WEBSITE_GENERIC"];
            }
            
            // todo: remove this once _website is weaned off
            if (string.IsNullOrEmpty(Manager.GetApplicationSettings().Configuration().ContactUri))
            {
                Manager.GetApplicationSettings().Configuration().ContactUri = Website;
            }

            InitializeMaps();

            WorkingDirectory = basepath.Value;
            this.Hostname = hostname;
            this.MaxClients = maxplayers;
            this.FSGame = game.Value;
            this.Gametype = gametype;
            this.IP = ip.Value == "localhost" ? ServerConfig.IPAddress : ip.Value ?? ServerConfig.IPAddress;
            this.GamePassword = gamePassword.Value;
            UpdateMap(mapname);

            if (RconParser.CanGenerateLogPath)
            {
                bool needsRestart = false;

                if (logsync.Value == 0)
                {
                    await this.SetDvarAsync("g_logsync", 2); // set to 2 for continous in other games, clamps to 1 for IW4
                    needsRestart = true;
                }

                if (string.IsNullOrWhiteSpace(logfile.Value))
                {
                    logfile.Value = "games_mp.log";
                    await this.SetDvarAsync("g_log", logfile.Value);
                    needsRestart = true;
                }

                if (needsRestart)
                {  
                    // disabling this for the time being
                    /*Logger.WriteWarning("Game log file not properly initialized, restarting map...");
                    await this.ExecuteCommandAsync("map_restart");*/ 
                }

                // this DVAR isn't set until the a map is loaded
                await this.SetDvarAsync("logfile", 2);
            }

            CustomCallback = await ScriptLoaded();

            // they've manually specified the log path
            if (!string.IsNullOrEmpty(ServerConfig.ManualLogPath) || !RconParser.CanGenerateLogPath)
            {
                LogPath = ServerConfig.ManualLogPath;

                if (string.IsNullOrEmpty(LogPath) && !RconParser.CanGenerateLogPath)
                {
                    throw new ServerException(loc["SERVER_ERROR_REQUIRES_PATH"].FormatExt(GameName.ToString()));
                }
            }

            else
            {
                var logInfo = new LogPathGeneratorInfo()
                {
                    BaseGameDirectory = basegame.Value,
                    BasePathDirectory = basepath.Value,
                    GameDirectory = EventParser.Configuration.GameDirectory ?? "",
                    ModDirectory = game.Value ?? "",
                    LogFile = logfile.Value,
                    IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                };
                LogPath = GenerateLogPath(logInfo);
                ServerLogger.LogInformation("Game log information {@logInfo}", logInfo);


                if (!File.Exists(LogPath) && ServerConfig.GameLogServerUrl == null)
                {
                    Console.WriteLine(loc["SERVER_ERROR_DNE"].FormatExt(LogPath));
                    ServerLogger.LogCritical("Game log path does not exist {logPath}", LogPath);
                    throw new ServerException(loc["SERVER_ERROR_DNE"].FormatExt(LogPath));
                }
            }

            ServerLogger.LogInformation("Generated game log path is {logPath}", LogPath);
            LogEvent = new GameLogEventDetection( _serviceProvider.GetRequiredService<ILogger<GameLogEventDetection>>(), 
                this, 
                GenerateUriForLog(LogPath, ServerConfig.GameLogServerUrl?.AbsoluteUri), gameLogReaderFactory);

            await _serverCache.InitializeAsync();
            _ = Task.Run(() => LogEvent.PollForChanges());

            if (!Utilities.IsDevelopment)
            {
                Broadcast(loc["BROADCAST_ONLINE"]);
            }
        }

        public Uri[] GenerateUriForLog(string logPath, string gameLogServerUrl)
        {
            var logUri = new Uri(logPath);

            if (string.IsNullOrEmpty(gameLogServerUrl))
            {
                return new[] { logUri };
            }

            else
            {
                return new[] { new Uri(gameLogServerUrl), logUri };
            }
        }

        public static string GenerateLogPath(LogPathGeneratorInfo logInfo)
        {
            string logPath;
            string workingDirectory = logInfo.BasePathDirectory;

            bool baseGameIsDirectory = !string.IsNullOrWhiteSpace(logInfo.BaseGameDirectory) &&
                logInfo.BaseGameDirectory.IndexOfAny(Utilities.DirectorySeparatorChars) != -1;

            bool baseGameIsRelative = logInfo.BaseGameDirectory.FixDirectoryCharacters()
                .Equals(logInfo.GameDirectory.FixDirectoryCharacters(), StringComparison.InvariantCultureIgnoreCase);

            // we want to see if base game is provided and it 'looks' like a directory
            if (baseGameIsDirectory && !baseGameIsRelative)
            {
                workingDirectory = logInfo.BaseGameDirectory;
            }

            if (string.IsNullOrWhiteSpace(logInfo.ModDirectory))
            {
                logPath = Path.Combine(workingDirectory, logInfo.GameDirectory, logInfo.LogFile);
            }

            else
            {
                logPath = Path.Combine(workingDirectory, logInfo.ModDirectory, logInfo.LogFile);
            }

            // fix wine drive name mangling
            if (!logInfo.IsWindows)
            {
                logPath = $"{Path.DirectorySeparatorChar}{Regex.Replace(logPath, @"[A-Z]:(\/|\\)", "")}";
            }

            return logPath.FixDirectoryCharacters();
        }

        public override async Task Warn(string reason, EFClient targetClient, EFClient targetOrigin)
        {
            // ensure player gets warned if command not performed on them in game
            targetClient = targetClient.ClientNumber < 0 ?
                Manager.GetActiveClients()
                .FirstOrDefault(c => c.ClientId == targetClient?.ClientId) ?? targetClient :
                targetClient;

            var newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Warning,
                Expires = DateTime.UtcNow,
                Offender = targetClient,
                Punisher = targetOrigin,
                Offense = reason,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating warn penalty for {targetClient}", targetClient.ToString());
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            if (targetClient.IsIngame)
            {
                if (targetClient.Warnings >= 4)
                {
                    targetClient.Kick(loc["SERVER_WARNLIMT_REACHED"], Utilities.IW4MAdminClient(this));
                    return;
                }

                // todo: move to translation sheet
                string message = $"^1{loc["SERVER_WARNING"]} ^7[^3{targetClient.Warnings}^7]: ^3{targetClient.Name}^7, {reason}";
                targetClient.CurrentServer.Broadcast(message);
            }
        }

        public override async Task Kick(string reason, EFClient targetClient, EFClient originClient, EFPenalty previousPenalty)
        {
            targetClient = targetClient.ClientNumber < 0 ?
                Manager.GetActiveClients()
                .FirstOrDefault(c => c.ClientId == targetClient?.ClientId) ?? targetClient :
                targetClient;

            var newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Kick,
                Expires = DateTime.UtcNow,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating kick penalty for {targetClient}", targetClient.ToString());
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            if (targetClient.IsIngame)
            {
                var e = new GameEvent()
                {
                    Type = GameEvent.EventType.PreDisconnect,
                    Origin = targetClient,
                    Owner = this
                };

                Manager.AddEvent(e);

                var formattedKick = string.Format(RconParser.Configuration.CommandPrefixes.Kick, 
                    targetClient.ClientNumber, 
                    _messageFormatter.BuildFormattedMessage(RconParser.Configuration, 
                        newPenalty, 
                        previousPenalty));
                await targetClient.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
        }

        public override async Task TempBan(string Reason, TimeSpan length, EFClient targetClient, EFClient originClient)
        {
            // ensure player gets kicked if command not performed on them in the same server
            targetClient = targetClient.ClientNumber < 0 ?
                Manager.GetActiveClients()
                .FirstOrDefault(c => c.ClientId == targetClient?.ClientId) ?? targetClient :
                targetClient;

            var newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.TempBan,
                Expires = DateTime.UtcNow + length,
                Offender = targetClient,
                Offense = Reason,
                Punisher = originClient,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating tempban penalty for {targetClient}", targetClient.ToString());
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            if (targetClient.IsIngame)
            {
                var formattedKick = string.Format(RconParser.Configuration.CommandPrefixes.Kick,
                    targetClient.ClientNumber,
                    _messageFormatter.BuildFormattedMessage(RconParser.Configuration, newPenalty));
                ServerLogger.LogDebug("Executing tempban kick command for {targetClient}", targetClient.ToString());
                await targetClient.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
        }

        public override async Task Ban(string reason, EFClient targetClient, EFClient originClient, bool isEvade = false)
        {
            // ensure player gets kicked if command not performed on them in the same server
            targetClient = targetClient.ClientNumber < 0 ?
                Manager.GetActiveClients()
                .FirstOrDefault(c => c.ClientId == targetClient?.ClientId) ?? targetClient :
                targetClient;

            EFPenalty newPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Ban,
                Expires = null,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                Link = targetClient.AliasLink,
                IsEvadedOffense = isEvade
            };

            ServerLogger.LogDebug("Creating ban penalty for {targetClient}", targetClient.ToString());
            targetClient.SetLevel(Permission.Banned, originClient);
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            if (targetClient.IsIngame)
            {
                ServerLogger.LogDebug("Attempting to kicking newly banned client {targetClient}", targetClient.ToString());
                var formattedString = string.Format(RconParser.Configuration.CommandPrefixes.Kick, 
                    targetClient.ClientNumber, 
                    _messageFormatter.BuildFormattedMessage(RconParser.Configuration, newPenalty));
                await targetClient.CurrentServer.ExecuteCommandAsync(formattedString);
            }
        }

        override public async Task Unban(string reason, EFClient Target, EFClient Origin)
        {
            var unbanPenalty = new EFPenalty()
            {
                Type = EFPenalty.PenaltyType.Unban,
                Expires = DateTime.Now,
                Offender = Target,
                Offense = reason,
                Punisher = Origin,
                When = DateTime.UtcNow,
                Active = true,
                Link = Target.AliasLink
            };

            ServerLogger.LogDebug("Creating unban penalty for {targetClient}", Target.ToString());
            Target.SetLevel(Permission.User, Origin);
            await Manager.GetPenaltyService().RemoveActivePenalties(Target.AliasLink.AliasLinkId);
            await Manager.GetPenaltyService().Create(unbanPenalty);
        }

        override public void InitializeTokens()
        {
            Manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYERS", (Server s) => Task.Run(async () => (await Manager.GetClientService().GetTotalClientsAsync()).ToString())));
            Manager.GetMessageTokens().Add(new MessageToken("VERSION", (Server s) => Task.FromResult(Application.Program.Version.ToString())));
            Manager.GetMessageTokens().Add(new MessageToken("NEXTMAP", (Server s) => SharedLibraryCore.Commands.NextMapCommand.GetNextMap(s, _translationLookup)));
            Manager.GetMessageTokens().Add(new MessageToken("ADMINS", (Server s) => Task.FromResult(SharedLibraryCore.Commands.ListAdminsCommand.OnlineAdmins(s, _translationLookup))));
        }
    }
}
