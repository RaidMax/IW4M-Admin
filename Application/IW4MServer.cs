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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
using IW4MAdmin.Application.Alerts;
using IW4MAdmin.Application.Commands;
using IW4MAdmin.Application.Plugin.Script;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Alerts;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces.Events;
using static Data.Models.Client.EFClient;

namespace IW4MAdmin
{
    public class IW4MServer : Server
    {
        private static readonly SharedLibraryCore.Localization.TranslationLookup loc = Utilities.CurrentLocalization.LocalizationIndex;
        public GameLogEventDetection LogEvent;
        private readonly ITranslationLookup _translationLookup;
        private readonly IMetaServiceV2 _metaService;
        private const int REPORT_FLAG_COUNT = 4;
        private long lastGameTime = 0;

        private readonly IServiceProvider _serviceProvider;
        private readonly IClientNoticeMessageFormatter _messageFormatter;
        private readonly ILookupCache<EFServer> _serverCache;
        private readonly CommandConfiguration _commandConfiguration;
        private EFServer _cachedDatabaseServer;
        private readonly StatManager _statManager;
        private readonly ApplicationConfiguration _appConfig;

        public IW4MServer(
            ServerConfiguration serverConfiguration,
            CommandConfiguration commandConfiguration,
            ITranslationLookup lookup,
            IMetaServiceV2 metaService, 
            IServiceProvider serviceProvider,
            IClientNoticeMessageFormatter messageFormatter,
            ILookupCache<EFServer> serverCache) : base(serviceProvider.GetRequiredService<ILogger<Server>>(), 
#pragma warning disable CS0612
            serviceProvider.GetRequiredService<SharedLibraryCore.Interfaces.ILogger>(), 
#pragma warning restore CS0612
            serverConfiguration,
            serviceProvider.GetRequiredService<IManager>(), 
            serviceProvider.GetRequiredService<IRConConnectionFactory>(),
            serviceProvider.GetRequiredService<IGameLogReaderFactory>(), serviceProvider)
        {
            _translationLookup = lookup;
            _metaService = metaService;
            _serviceProvider = serviceProvider;
            _messageFormatter = messageFormatter;
            _serverCache = serverCache;
            _commandConfiguration = commandConfiguration;
            _statManager = serviceProvider.GetRequiredService<StatManager>();
            _appConfig =  serviceProvider.GetService<ApplicationConfiguration>();
            
            IGameServerEventSubscriptions.MonitoringStarted += async (gameEvent, token) =>
            {
                if (gameEvent.Server.Id != Id)
                {
                    return;
                }

                await EnsureServerAdded();
                await _statManager.EnsureServerAdded(gameEvent.Server, token);
            };
        }

        public override async Task<EFClient> OnClientConnected(EFClient clientFromLog)
        {
            ServerLogger.LogDebug("Client slot #{clientNumber} now reserved", clientFromLog.ClientNumber);

            var client = await Manager.GetClientService().GetUnique(clientFromLog.NetworkId, GameName);

            // first time client is connecting to server
            if (client == null)
            {
                ServerLogger.LogDebug("Client {client} first time connecting", clientFromLog.ToString());
                clientFromLog.CurrentServer = this;
                client = await Manager.GetClientService().Create(clientFromLog);
            }

            client.CopyAdditionalProperties(clientFromLog);

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
            client.Team = clientFromLog.Team;
            client.TeamName = clientFromLog.TeamName;
            client.CurrentServer = this;
            client.State = ClientState.Connecting;

            Clients[client.ClientNumber] = client;
            ServerLogger.LogDebug("End PreConnect for {client}", client.ToString());
            var e = new GameEvent
            {
                Origin = client,
                Owner = this,
                Type = GameEvent.EventType.Connect
            };

            Manager.AddEvent(e);
            Manager.QueueEvent(new ClientStateInitializeEvent
            {
                Client = client,
                Source = this,
            });
            return client;
        }

        public override async Task OnClientDisconnected(EFClient client)
        {
            if (GetClientsAsList().All(eachClient => eachClient.NetworkId != client.NetworkId))
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
                    await E.Origin.Lock();
                }

                try
                {
                    if (!await ProcessEvent(E))
                    {
                        return;
                    }

                    Command command = null;
                    if (E.Type == GameEvent.EventType.Command)
                    {
                        try
                        {
                            command = await SharedLibraryCore.Commands.CommandProcessing.ValidateCommand(E, Manager.GetApplicationSettings().Configuration(), _commandConfiguration);
                        }

                        catch (CommandException e)
                        {
                            ServerLogger.LogWarning(e, "Error validating command from event {@Event}", 
                                new { E.Type, E.Data, E.Message, E.Subtype, E.IsRemote, E.CorrelationId });
                            E.FailReason = GameEvent.EventFailReason.Invalid;
                        }

                        if (command != null)
                        {
                            E.Extra = command;
                        }
                    }

                    var canExecuteCommand = Manager.CommandInterceptors.All(interceptor =>
                    {
                        try
                        {
                            return interceptor(E);
                        }
                        catch
                        {
                            return true;
                        }
                    });

                    if (!canExecuteCommand)
                    {
                        E.Origin.Tell(_translationLookup["SERVER_COMMANDS_INTERCEPTED"]);
                    }

                    else if (E.Type == GameEvent.EventType.Command && E.Extra is Command cmd)
                    {
                        ServerLogger.LogInformation("Executing command {Command} for {Client}", cmd.Name,
                            E.Origin.ToString());
                        await cmd.ExecuteAsync(E);
                        Manager.QueueEvent(new ClientExecuteCommandEvent
                        {
                            Command = cmd,
                            Client = E.Origin,
                            Source = this,
                            CommandText = E.Data
                        });
                    }

                    var pluginTasks = Manager.Plugins.Where(plugin => !plugin.IsParser)
                        .Select(plugin => CreatePluginTask(plugin, E));
                    
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
            if (plugin is ScriptPlugin { IsParser: true })
            {
                return;
            }

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(Utilities.DefaultCommandTimeout);

            try
            {
                await plugin.OnEventAsync(gameEvent, this);
            }
            catch (OperationCanceledException)
            {
                ServerLogger.LogWarning("Timed out executing event {EventType} for {Plugin}", gameEvent.Type,
                    plugin.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine(loc["SERVER_PLUGIN_ERROR"].FormatExt(plugin.Name, ex.GetType().Name));
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

                if (E.Type == GameEvent.EventType.ConnectionLost)
                {
                    var exception = E.Extra as Exception;
                    ServerLogger.LogError(exception,
                        "Connection lost with {server}", ToString());

                    if (!Manager.GetApplicationSettings().Configuration().IgnoreServerConnectionLost)
                    {
                        Console.WriteLine(loc["SERVER_ERROR_COMMUNICATION"].FormatExt($"{ListenAddress}:{ListenPort}"));
                        
                        var alert = Alert.AlertState.Build().OfType(E.Type.ToString())
                            .WithCategory(Alert.AlertCategory.Error)
                            .FromSource("System")
                            .WithMessage(loc["SERVER_ERROR_COMMUNICATION"].FormatExt($"{ListenAddress}:{ListenPort}"))
                            .ExpiresIn(TimeSpan.FromDays(1));
                        
                        Manager.AlertManager.AddAlert(alert);
                    }
      
                    Throttled = true;
                }

                else if (E.Type == GameEvent.EventType.ConnectionRestored)
                {
                    ServerLogger.LogInformation(
                        "Connection restored with {Server}", ToString());
                    
                    if (!Manager.GetApplicationSettings().Configuration().IgnoreServerConnectionLost)
                    {
                        Console.WriteLine(loc["MANAGER_CONNECTION_REST"].FormatExt($"{ListenAddress}:{ListenPort}"));
                        
                        var alert = Alert.AlertState.Build().OfType(E.Type.ToString())
                            .WithCategory(Alert.AlertCategory.Information)
                            .FromSource("System")
                            .WithMessage(loc["MANAGER_CONNECTION_REST"].FormatExt($"{ListenAddress}:{ListenPort}"))
                            .ExpiresIn(TimeSpan.FromDays(1));

                        Manager.AlertManager.AddAlert(alert);
                    }

                    if (!string.IsNullOrEmpty(CustomSayName))
                    {
                        await this.SetDvarAsync("sv_sayname", CustomSayName, Manager.CancellationToken);
                    }

                    Throttled = false;
                }

                else if (E.Type == GameEvent.EventType.ChangePermission)
                {
                    var newPermission = (Permission) E.Extra;
                    var oldPermission = E.Target.Level;
                    ServerLogger.LogInformation("{origin} is setting {target} to permission level {newPermission}",
                        E.Origin.ToString(), E.Target.ToString(), newPermission);
                    await Manager.GetClientService().UpdateLevel(newPermission, E.Target, E.Origin);
                    
                    Manager.QueueEvent(new ClientPermissionChangeEvent
                    {
                        Client = E.Origin,
                        Source = this, 
                        OldPermission = oldPermission,
                        NewPermission = newPermission
                    });
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

                        var clientTag = await _metaService.GetPersistentMetaByLookup(EFMeta.ClientTagV2,
                            EFMeta.ClientTagNameV2, E.Origin.ClientId, Manager.CancellationToken);

                        if (clientTag?.Value != null)
                        {
                            E.Origin.Tag = clientTag.Value;
                        }

                        try
                        {
                            var factory = _serviceProvider.GetRequiredService<IDatabaseContextFactory>();
                            await using var context = factory.CreateContext(enableTracking: false);

                            var messageCount = await context.InboxMessages
                                .CountAsync(msg => msg.DestinationClientId == E.Origin.ClientId && !msg.IsDelivered);
      
                            if (messageCount > 0)
                            {
                                E.Origin.Tell(_translationLookup["SERVER_JOIN_OFFLINE_MESSAGES"]);
                            }
                        }
                        catch (Exception ex)
                        {
                            ServerLogger.LogError(ex, "Could not get offline message count for {Client}", E.Origin.ToString());
                            throw;
                        }
                        
                        await E.Origin.OnJoin(E.Origin.IPAddress, Manager.GetApplicationSettings().Configuration().EnableImplicitAccountLinking);
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
                            E.Origin.GameName = (Reference.Game)GameName;
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
                            E.Origin.Tell(loc["SERVER_REPORT_COUNT_V2"].FormatExt(E.Owner.Reports.Count));
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

                    await Manager.GetPenaltyService().Create(newPenalty);
                    E.Target.SetLevel(Permission.Flagged, E.Origin);
                    
                    Manager.QueueEvent(new ClientPenaltyEvent
                    {
                        Client = E.Target,
                        Penalty = newPenalty
                    });
                }

                else if (E.Type == GameEvent.EventType.Unflag)
                {
                    var unflagPenalty = new EFPenalty
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
                    await Manager.GetPenaltyService().RemoveActivePenalties(E.Target.AliasLinkId, E.Target.NetworkId,
                        E.Target.GameName, E.Target.CurrentAlias?.IPAddress, new[] {EFPenalty.PenaltyType.Flag});
                    await Manager.GetPenaltyService().Create(unflagPenalty);
                    
                    Manager.QueueEvent(new ClientPenaltyRevokeEvent
                    {
                        Client = E.Target,
                        Penalty = unflagPenalty
                    });
                }

                else if (E.Type == GameEvent.EventType.Report)
                {
                    Reports.Add(new Report()
                    {
                        Origin = E.Origin,
                        Target = E.Target,
                        Reason = E.Data,
                        ReportedOn = DateTime.UtcNow
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
                    
                    Manager.QueueEvent(new ClientPenaltyEvent
                    {
                        Client = E.Target,
                        Penalty = newReport,
                        Source = this
                    });
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

                    await _metaService.SetPersistentMeta("LastMapPlayed", CurrentMap.Alias, E.Origin.ClientId);
                    await _metaService.SetPersistentMeta("LastServerPlayed", E.Owner.Hostname, E.Origin.ClientId);
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

                else if (E.Type == GameEvent.EventType.Say)
                {
                    if (E.Data?.Length > 0)
                    {
                        string message = E.Data;
                        if (E.Data.IsQuickMessage())
                        {
                            try
                            {
                                message = _serviceProvider.GetRequiredService<DefaultSettings>()
                                    .QuickMessages
                                    .First(_qm => _qm.Game == GameName)
                                    .Messages[E.Data.Substring(1)];
                            }
                            catch
                            {
                                message = E.Data.Substring(1);
                            }
                        }

                        ChatHistory.Add(new ChatInfo
                        {
                            Name = E.Origin.Name,
                            Message = message,
                            Time = DateTime.UtcNow,
                            IsHidden = !string.IsNullOrEmpty(GamePassword)
                        });
                    }
                }

                else if (E.Type == GameEvent.EventType.MapChange)
                {
                    ServerLogger.LogInformation("New map loaded - {ClientCount} active players", ClientNum);

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
                            if (dict.ContainsKey("gametype"))
                            {
                                UpdateGametype(dict["gametype"]);
                            }

                            if (dict.ContainsKey("hostname"))
                            {
                                UpdateHostname(dict["hostname"]);
                            }

                            var newMapName = dict.ContainsKey("mapname")
                                ? dict["mapname"] ?? CurrentMap.Name
                                : CurrentMap.Name;
                            UpdateMap(newMapName);
                        }
                    }

                    else
                    {
                        var dict = (Dictionary<string, string>)E.Extra;
                        
                        if (dict.ContainsKey("g_gametype"))
                        {
                            UpdateGametype(dict["g_gametype"]);
                        }

                        if (dict.ContainsKey("sv_hostname"))
                        {
                            UpdateHostname(dict["sv_hostname"]);
                        }

                        if (dict.ContainsKey("sv_maxclients"))
                        {
                            UpdateMaxPlayers(int.Parse(dict["sv_maxclients"]));
                        }

                        else if (dict.ContainsKey("com_maxclients"))
                        {
                            UpdateMaxPlayers(int.Parse(dict["com_maxclients"]));
                        }
                        
                        else if (dict.ContainsKey("com_maxplayers"))
                        {
                            UpdateMaxPlayers(int.Parse(dict["com_maxplayers"]));
                        }

                        if (dict.ContainsKey("mapname"))
                        {
                            UpdateMap(dict["mapname"]);
                        }
                    }

                    if (E.GameTime.HasValue)
                    {
                        lastGameTime = E.GameTime.Value;
                    }

                    MatchStartTime = DateTime.Now;
                }

                else if (E.Type == GameEvent.EventType.MapEnd)
                {
                    ServerLogger.LogInformation("Game ending...");

                    if (E.GameTime.HasValue)
                    {
                        lastGameTime = E.GameTime.Value;
                    }

                    MatchEndTime = DateTime.Now;
                }

                else if (E.Type == GameEvent.EventType.Tell)
                {
                    await Tell(E.Message, E.Target);
                }

                else if (E.Type == GameEvent.EventType.Broadcast)
                {
                    if (!Utilities.IsDevelopment && E.Data != null) // hides broadcast when in development mode
                    {
                        await E.Owner.ExecuteCommandAsync(E.Data);
                    }
                }
                
                else if (E.Type == GameEvent.EventType.JoinTeam)
                {
                    E.Origin.UpdateTeam(E.Extra as string);
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

        public async Task EnsureServerAdded()
        {
            var gameServer = await _serverCache
                .FirstAsync(server => server.EndPoint == base.Id);
            
            if (gameServer == null)
            {
                gameServer = new EFServer
                {
                    Port = ListenPort,
                    EndPoint = base.Id,
                    ServerId = BuildLegacyDatabaseId(),
                    GameName = (Reference.Game?)GameName,
                    HostName = ServerName
                };

                await _serverCache.AddAsync(gameServer);
            }

            await using var context = _serviceProvider.GetRequiredService<IDatabaseContextFactory>()
                .CreateContext(enableTracking: false);

            context.Servers.Attach(gameServer);

            // we want to set the gamename up if it's never been set, or it changed
            if (!gameServer.GameName.HasValue || gameServer.GameName.Value != GameCode)
            {
                gameServer.GameName = GameCode;
                context.Entry(gameServer).Property(property => property.GameName).IsModified = true;
            }

            if (gameServer.HostName == null || gameServer.HostName != ServerName)
            {
                gameServer.HostName = ServerName;
                context.Entry(gameServer).Property(property => property.HostName).IsModified = true;
            }

            if (gameServer.IsPasswordProtected != !string.IsNullOrEmpty(GamePassword))
            {
                gameServer.IsPasswordProtected = !string.IsNullOrEmpty(GamePassword);
                context.Entry(gameServer).Property(property => property.IsPasswordProtected).IsModified = true;
            }

            await context.SaveChangesAsync();
            _cachedDatabaseServer = gameServer;
        }

        private async Task OnClientUpdate(EFClient origin)
        {
            var client = GetClientsAsList().FirstOrDefault(c => c.NetworkId == origin.NetworkId);

            if (client == null)
            {
                ServerLogger.LogWarning("{Origin} expected to exist in client list for update, but they do not", origin.ToString());
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
                    await client.OnJoin(origin.IPAddress, Manager.GetApplicationSettings().Configuration().EnableImplicitAccountLinking);
                }

                catch (Exception e)
                {
                    using(LogContext.PushProperty("Server", ToString()))
                    {
                        ServerLogger.LogError(e, "Could not execute on join for {origin}", origin.ToString());
                    }
                }
            }

            else if (client.IPAddress != null && client.State == ClientState.Disconnecting ||
                client.Level == Permission.Banned)
            {
                ServerLogger.LogWarning("{Client} state is Unknown (probably kicked), but they are still connected. trying to kick again...", origin.ToString());
                await client.CanConnect(client.IPAddress, Manager.GetApplicationSettings().Configuration().EnableImplicitAccountLinking);
            }
        }

        /// <summary>
        /// lists the connecting and disconnecting clients via RCon response
        /// array index 0 = connecting clients
        /// array index 1 = disconnecting clients
        /// array index 2 = updated clients
        /// </summary>
        /// <returns></returns>
        async Task<List<EFClient>[]> PollPlayersAsync(CancellationToken token)
        {
            if (DateTime.Now - (MatchEndTime ?? MatchStartTime) < TimeSpan.FromSeconds(15))
            {
                ServerLogger.LogDebug("Skipping status poll attempt because the match ended recently");
                return null;
            }
 
            var currentClients = GetClientsAsList();
            var statusResponse = await this.GetStatusAsync(token);

            if (statusResponse is null)
            {
                return null;
            }

            var polledClients = statusResponse.Clients.AsEnumerable();

            if (Manager.GetApplicationSettings().Configuration().IgnoreBots)
            {
                polledClients = polledClients.Where(c => !c.IsBot);
            }
            var disconnectingClients = currentClients.Except(polledClients);
            var connectingClients = polledClients.Except(currentClients);
            var updatedClients = polledClients.Except(connectingClients).Except(disconnectingClients);

            UpdateMap(statusResponse.Map);
            UpdateGametype(statusResponse.GameType);
            UpdateHostname(statusResponse.Hostname);
            UpdateMaxPlayers(statusResponse.MaxClients);

            return new []
            {
                connectingClients.ToList(),
                disconnectingClients.ToList(),
                updatedClients.ToList()
            };
        }
        
        public override async Task<long> GetIdForServer(Server server = null)
        {
            server ??= this;

            return (await _serverCache.FirstAsync(cachedServer =>
                cachedServer.EndPoint == server.Id || cachedServer.ServerId == server.EndPoint)).ServerId;
        }

        private long BuildLegacyDatabaseId()
        {
            long id = HashCode.Combine(ListenAddress, ListenPort);
            return id < 0 ? Math.Abs(id) : id;
        }

        private void UpdateMap(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
            {
                return;
            }
            
            var foundMap = Maps.Find(m => m.Name == mapName) ?? new Map
            {
                Alias = mapName,
                Name = mapName
            };

            if (foundMap == CurrentMap)
            {
                return;
            }

            CurrentMap = foundMap;
            
            using(LogContext.PushProperty("Server", Id))
            {
                ServerLogger.LogDebug("Updating map to {@CurrentMap}", CurrentMap);
            }
        }

        private void UpdateGametype(string gameType)
        {
            if (string.IsNullOrEmpty(gameType))
            {
                return;
            }
            
            Gametype = gameType;

            using(LogContext.PushProperty("Server", Id))
            {
                ServerLogger.LogDebug("Updating gametype to {Gametype}", gameType);
            }
        }

        private void UpdateHostname(string hostname)
        {
            if (string.IsNullOrEmpty(hostname) || Hostname == hostname)
            {
                return;
            }
            
            using(LogContext.PushProperty("Server", Id))
            {
                ServerLogger.LogDebug("Updating hostname to {HostName}", hostname);
            }

            Hostname = hostname;
        }

        private void UpdateMaxPlayers(int? maxPlayers)
        {
            if (maxPlayers == null || maxPlayers == MaxClients)
            {
                return;
            }
            
            using(LogContext.PushProperty("Server", Id))
            {
                ServerLogger.LogDebug("Updating max clients to {MaxPlayers}", maxPlayers);
            }

            MaxClients = maxPlayers.Value;
        }
        
        private async Task ShutdownInternal()
        {
            foreach (var client in GetClientsAsList())
            {
                await client.OnDisconnect();

                var e = new GameEvent
                {
                    Type = GameEvent.EventType.Disconnect,
                    Owner = this,
                    Origin = client
                };

                Manager.AddEvent(e);

                await e.WaitAsync(Utilities.DefaultCommandTimeout, new CancellationTokenRegistration().Token);
            }

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(Utilities.DefaultCommandTimeout);
            
            Manager.QueueEvent(new MonitorStopEvent
            {
                Server = this
            });
        }

        private DateTime _lastMessageSent = DateTime.Now;
        private DateTime _lastPlayerCount = DateTime.Now;

        public override async Task<bool> ProcessUpdatesAsync(CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                {
                    await ShutdownInternal();
                    return true;
                }

                try
                {
                    if (Manager.GetApplicationSettings().Configuration().RConPollRate == int.MaxValue &&
                        Utilities.IsDevelopment)
                    {
                        return true;
                    }

                    var polledClients = await PollPlayersAsync(token);

                    if (polledClients is null)
                    {
                        return true;
                    }

                    foreach (var disconnectingClient in polledClients[1]
                                 .Where(client => !client.IsZombieClient /* ignores "fake" zombie clients */))
                    {
                        disconnectingClient.CurrentServer = this;
                        var e = new GameEvent
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
                    foreach (var client in polledClients[0].Where(client =>
                                 !string.IsNullOrEmpty(client.Name) && (client.Ping != 999 || client.IsBot)))
                    {
                        client.CurrentServer = this;
                        client.GameName = (Reference.Game)GameName;
                        
                        var e = new GameEvent
                        {
                            Type = GameEvent.EventType.PreConnect,
                            Origin = client,
                            Owner = this,
                            IsBlocking = true,
                            Extra = client.GetAdditionalProperty<string>("BotGuid"),
                            Source = GameEvent.EventSource.Status,
                        };

                        Manager.AddEvent(e);
                        await e.WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken);
                    }

                    // these are the clients that have updated
                    foreach (var client in polledClients[2])
                    {
                        client.CurrentServer = this;
                        var gameEvent = new GameEvent
                        {
                            Type = GameEvent.EventType.Update,
                            Origin = client,
                            Owner = this
                        };

                        Manager.AddEvent(gameEvent);
                    }

                    if (polledClients[2].Any())
                    {
                        Manager.QueueEvent(new ClientDataUpdateEvent
                        {
                            Clients = new ReadOnlyCollection<EFClient>(polledClients[2]),
                            Server = this,
                            Source = this,
                        });
                    }

                    if (Throttled)
                    {
                        var gameEvent = new GameEvent
                        {
                            Type = GameEvent.EventType.ConnectionRestored,
                            Owner = this,
                            Origin = Utilities.IW4MAdminClient(this),
                            Target = Utilities.IW4MAdminClient(this)
                        };

                        Manager.AddEvent(gameEvent);
                        
                        Manager.QueueEvent(new ConnectionRestoreEvent
                        {
                            Server = this,
                            Source = this
                        });
                    }

                    LastPoll = DateTime.Now;
                }

                catch (NetworkException ex)
                {
                    if (Throttled)
                    {
                        return true;
                    }

                    var gameEvent = new GameEvent
                    {
                        Type = GameEvent.EventType.ConnectionLost,
                        Owner = this,
                        Origin = Utilities.IW4MAdminClient(this),
                        Target = Utilities.IW4MAdminClient(this),
                        Extra = ex,
                        Data = ConnectionErrors.ToString()
                    };

                    Manager.AddEvent(gameEvent);
                    Manager.QueueEvent(new ConnectionInterruptEvent
                    {
                        Server = this,
                        Source = this
                    });
                    
                    return true;
                }
                finally
                {
                    RunServerCollection();
                }

                if (DateTime.Now - _lastMessageSent <=
                    TimeSpan.FromSeconds(Manager.GetApplicationSettings().Configuration().AutoMessagePeriod) ||
                    BroadcastMessages.Count <= 0 || ClientNum <= 0)
                {
                    return true;
                }

                // send out broadcast messages
                var messages =
                    (await this.ProcessMessageToken(Manager.GetMessageTokens(), BroadcastMessages[NextMessage])).Split(
                        Environment.NewLine);
                await BroadcastAsync(messages, token: Manager.CancellationToken);

                NextMessage = NextMessage == BroadcastMessages.Count - 1 ? 0 : NextMessage + 1;
                _lastMessageSent = DateTime.Now;

                return true;
            }

            catch (TaskCanceledException)
            {
                await ShutdownInternal();
                return true;
            }

            // this one is ok
            catch (Exception e) when (e is ServerException || e is RConException)
            {
                using (LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogWarning(e, "Undesirable exception occured during processing updates");
                }

                return false;
            }

            catch (Exception e)
            {
                using (LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogError(e, "Unexpected exception occured during processing updates");
                }

                Console.WriteLine(loc["SERVER_ERROR_EXCEPTION"].FormatExt($"[{ListenAddress}:{ListenPort}]"));
                return false;
            }
        }

        private void RunServerCollection()
        {
            if (DateTime.Now - _lastPlayerCount < _appConfig?.ServerDataCollectionInterval)
            {
                return;
            }

            var maxItems = Math.Ceiling(_appConfig!.MaxClientHistoryTime.TotalMinutes /
                                        _appConfig.ServerDataCollectionInterval.TotalMinutes);
                    
            while (ClientHistory.ClientCounts.Count > maxItems) 
            {
                ClientHistory.ClientCounts.RemoveAt(0);
            }

            ClientHistory.ClientCounts.Add(new ClientCountSnapshot
            {
                ClientCount = ClientNum,
                ConnectionInterrupted = Throttled,
                Time = DateTime.UtcNow,
                Map = CurrentMap.Name
            });
            
            _lastPlayerCount = DateTime.Now;
        }

        public async Task Initialize()
        {
            try
            {
                ResolvedIpEndPoint =
                    new IPEndPoint(
                        (await Dns.GetHostAddressesAsync(ListenAddress)).First(address =>
                            address.AddressFamily == AddressFamily.InterNetwork), ListenPort);
            }
            catch (Exception ex)
            {
                ServerLogger.LogWarning(ex, "Could not resolve hostname or IP for RCon connection {Address}:{Port}", ListenAddress, ListenPort);
                ResolvedIpEndPoint = new IPEndPoint(IPAddress.Parse(ListenAddress), ListenPort);
            }

            RconParser = Manager.AdditionalRConParsers
                .FirstOrDefault(parser =>
                    parser.Version == ServerConfig.RConParserVersion ||
                    parser.Name == ServerConfig.RConParserVersion);

            EventParser = Manager.AdditionalEventParsers
                .FirstOrDefault(parser =>
                    parser.Version == ServerConfig.EventParserVersion ||
                    parser.Name == ServerConfig.RConParserVersion);

            RconParser ??= Manager.AdditionalRConParsers[0];
            EventParser ??= Manager.AdditionalEventParsers[0];

            RemoteConnection = RConConnectionFactory.CreateConnection(ResolvedIpEndPoint, Password, RconParser.RConEngine);
            RemoteConnection.SetConfiguration(RconParser);

            var version = await this.GetMappedDvarValueOrDefaultAsync<string>("version", token: Manager.CancellationToken);
            Version = version.Value;
            GameName = Utilities.GetGame(version.Value ?? RconParser.Version);

            if (GameName == Game.UKN)
            {
                GameName = RconParser.GameName;
            }

            if (version.Value?.Length != 0)
            {
                var matchedRconParser = Manager.AdditionalRConParsers.FirstOrDefault(_parser => _parser.Version == version.Value);
                RconParser.Configuration = matchedRconParser != null ? matchedRconParser.Configuration : RconParser.Configuration;
                EventParser = Manager.AdditionalEventParsers.FirstOrDefault(_parser => _parser.Version == version.Value) ?? EventParser;
                Version = RconParser.Version;
            }

            var svRunning = await this.GetMappedDvarValueOrDefaultAsync<string>("sv_running", token: Manager.CancellationToken);

            if (!string.IsNullOrEmpty(svRunning.Value) && svRunning.Value != "1")
            {
                throw new ServerException(loc["SERVER_ERROR_NOT_RUNNING"].FormatExt(this.ToString()));
            }

            var infoResponse = RconParser.Configuration.CommandPrefixes.RConGetInfo != null ? await this.GetInfoAsync() : null;

            var hostname = (await this.GetMappedDvarValueOrDefaultAsync<string>("sv_hostname", "hostname", infoResponse, token: Manager.CancellationToken)).Value;
            var mapname = (await this.GetMappedDvarValueOrDefaultAsync<string>("mapname", infoResponse: infoResponse, token: Manager.CancellationToken)).Value;
            var maxplayers = (await this.GetMappedDvarValueOrDefaultAsync<int>("sv_maxclients", infoResponse: infoResponse, token: Manager.CancellationToken)).Value;
            var gametype = (await this.GetMappedDvarValueOrDefaultAsync<string>("g_gametype", "gametype", infoResponse, token: Manager.CancellationToken)).Value;
            var basepath = await this.GetMappedDvarValueOrDefaultAsync<string>("fs_basepath", token: Manager.CancellationToken);
            var basegame = await this.GetMappedDvarValueOrDefaultAsync<string>("fs_basegame", token: Manager.CancellationToken);
            var homepath = await this.GetMappedDvarValueOrDefaultAsync<string>("fs_homepath", token: Manager.CancellationToken);
            var game = await this.GetMappedDvarValueOrDefaultAsync<string>("fs_game", infoResponse: infoResponse, token: Manager.CancellationToken);
            var logfile = await this.GetMappedDvarValueOrDefaultAsync<string>("g_log", token: Manager.CancellationToken);
            var logsync = await this.GetMappedDvarValueOrDefaultAsync<int>("g_logsync", token: Manager.CancellationToken);
            var ip = await this.GetMappedDvarValueOrDefaultAsync<string>("net_ip", token: Manager.CancellationToken);
            var gamePassword = await this.GetMappedDvarValueOrDefaultAsync("g_password", overrideDefault: "", token: Manager.CancellationToken);
            var privateClients = await this.GetMappedDvarValueOrDefaultAsync("sv_privateClients", overrideDefault: 0,
                token: Manager.CancellationToken);

            if (Manager.GetApplicationSettings().Configuration().EnableCustomSayName)
            {
                await this.SetDvarAsync("sv_sayname", CustomSayName,
                    Manager.CancellationToken);
            }

            try
            {
                var website = await this.GetMappedDvarValueOrDefaultAsync<string>("_website", token: Manager.CancellationToken);

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
            
            var defaultConfig = _serviceProvider.GetRequiredService<DefaultSettings>();
            var gameMaps = defaultConfig?.Maps?.FirstOrDefault(map => map.Game == GameName);

            if (gameMaps != null)
            {
                Maps.AddRange(gameMaps.Maps);
            }

            WorkingDirectory = basepath.Value;
            Hostname = hostname;
            MaxClients = maxplayers;
            FSGame = game.Value;
            Gametype = gametype;
            IP = ip.Value is "localhost" or "0.0.0.0" ? ServerConfig.IPAddress : ip.Value ?? ServerConfig.IPAddress;
            GamePassword = gamePassword.Value;
            PrivateClientSlots = privateClients.Value;
            
            UpdateMap(mapname);

            if (RconParser.CanGenerateLogPath && string.IsNullOrEmpty(ServerConfig.ManualLogPath))
            {
                if (logsync.Value == 0)
                {
                    await this.SetDvarAsync("g_logsync", 2, Manager.CancellationToken); // set to 2 for continous in other games, clamps to 1 for IW4
                }

                if (string.IsNullOrWhiteSpace(logfile.Value))
                {
                    logfile.Value = "games_mp.log";
                    await this.SetDvarAsync("g_log", logfile.Value, Manager.CancellationToken);
                }

                // this DVAR isn't set until the a map is loaded
                await this.SetDvarAsync("logfile", 2, Manager.CancellationToken);
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
                    HomePathDirectory = homepath.Value,
                    GameDirectory = EventParser.Configuration.GameDirectory ?? "",
                    ModDirectory = game.Value ?? "",
                    LogFile = logfile.Value,
                    IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                    IsOneLog = RconParser.IsOneLog
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
            var logUri = new Uri(logPath, UriKind.Absolute);

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
            var workingDirectory = logInfo.BasePathDirectory;
            
            bool IsValidGamePath (string path)
            {
                var baseGameIsDirectory = !string.IsNullOrWhiteSpace(path) &&
                                          path.IndexOfAny(Utilities.DirectorySeparatorChars) != -1;

                var baseGameIsRelative = path.FixDirectoryCharacters()
                    .Equals(logInfo.GameDirectory.FixDirectoryCharacters(), StringComparison.InvariantCultureIgnoreCase);

                return baseGameIsDirectory && !baseGameIsRelative;
            }

            // we want to see if base game is provided and it 'looks' like a directory
            if (IsValidGamePath(logInfo.HomePathDirectory))
            {
                workingDirectory = logInfo.HomePathDirectory;
            }
            
            else if (IsValidGamePath(logInfo.BaseGameDirectory))
            {
                workingDirectory = logInfo.BaseGameDirectory;
            }

            if (string.IsNullOrWhiteSpace(logInfo.ModDirectory) || logInfo.IsOneLog)
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
            var activeClient = Manager.FindActiveClient(targetClient);

            var newPenalty = new EFPenalty
            {
                Type = EFPenalty.PenaltyType.Warning,
                Expires = DateTime.UtcNow,
                Offender = targetClient,
                Punisher = targetOrigin,
                Offense = reason,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating warn penalty for {TargetClient}", targetClient.ToString());
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            if (activeClient.IsIngame)
            {
                if (activeClient.Warnings >= 4)
                {
                    activeClient.Kick(loc["SERVER_WARNLIMT_REACHED"], Utilities.IW4MAdminClient(this));
                    return;
                }

                var message = loc["COMMANDS_WARNING_FORMAT_V2"]
                    .FormatExt(activeClient.Warnings, activeClient.Name, reason);
                activeClient.CurrentServer.Broadcast(message);
            }
            
            Manager.QueueEvent(new ClientPenaltyEvent
            {
                Client = targetClient,
                Penalty = newPenalty
            });
        }

        public override async Task Kick(string reason, EFClient targetClient, EFClient originClient, EFPenalty previousPenalty)
        {
            var activeClient = Manager.FindActiveClient(targetClient);

            var newPenalty = new EFPenalty
            {
                Type = EFPenalty.PenaltyType.Kick,
                Expires = DateTime.UtcNow,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating kick penalty for {TargetClient}", targetClient.ToString());
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            if (activeClient.IsIngame)
            {
                var gameEvent = new GameEvent
                {
                    Type = GameEvent.EventType.PreDisconnect,
                    Origin = activeClient,
                    Owner = this
                };

                Manager.AddEvent(gameEvent);

                var formattedKick = string.Format(RconParser.Configuration.CommandPrefixes.Kick, 
                    activeClient.TemporalClientNumber, 
                    _messageFormatter.BuildFormattedMessage(RconParser.Configuration, 
                        newPenalty, 
                        previousPenalty));
                ServerLogger.LogDebug("Executing tempban kick command for {ActiveClient}", activeClient.ToString());
                await activeClient.CurrentServer.ExecuteCommandAsync(formattedKick);
            }

            Manager.QueueEvent(new ClientPenaltyEvent
            {
                Client = targetClient,
                Penalty = newPenalty
            });
        }

        public override Task<string[]> ExecuteCommandAsync(string command, CancellationToken token = default) =>
            Utilities.ExecuteCommandAsync(this, command, token);

        public override Task SetDvarAsync(string name, object value, CancellationToken token = default) =>
            Utilities.SetDvarAsync(this, name, value, token);

        public override async Task TempBan(string reason, TimeSpan length, EFClient targetClient, EFClient originClient)
        {
            // ensure player gets kicked if command not performed on them in the same server
            var activeClient = Manager.FindActiveClient(targetClient);

            var newPenalty = new EFPenalty
            {
                Type = EFPenalty.PenaltyType.TempBan,
                Expires = DateTime.UtcNow + length,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating tempban penalty for {TargetClient}", targetClient.ToString());
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);
            
            foreach (var reports in Manager.GetServers().Select(server => server.Reports))
            {
                reports.RemoveAll(report => report.Target.ClientId == targetClient.ClientId);
            }

            if (activeClient.IsIngame)
            {
                var formattedKick = string.Format(RconParser.Configuration.CommandPrefixes.Kick,
                    activeClient.TemporalClientNumber,
                    _messageFormatter.BuildFormattedMessage(RconParser.Configuration, newPenalty));
                ServerLogger.LogDebug("Executing tempban kick command for {ActiveClient}", activeClient.ToString());
                await activeClient.CurrentServer.ExecuteCommandAsync(formattedKick);
            }
            
            Manager.QueueEvent(new ClientPenaltyEvent
            {
                Client = targetClient,
                Penalty = newPenalty
            });
        }

        public override async Task Ban(string reason, EFClient targetClient, EFClient originClient, bool isEvade = false)
        {
            // ensure player gets kicked if command not performed on them in the same server
            var activeClient = Manager.FindActiveClient(targetClient);

            var newPenalty = new EFPenalty
            {
                Type = EFPenalty.PenaltyType.Ban,
                Expires = null,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                IsEvadedOffense = isEvade
            };

            ServerLogger.LogDebug("Creating ban penalty for {TargetClient}", targetClient.ToString());
            activeClient.SetLevel(Permission.Banned, originClient);
            await newPenalty.TryCreatePenalty(Manager.GetPenaltyService(), ServerLogger);

            foreach (var reports in Manager.GetServers().Select(server => server.Reports))
            {
                reports.RemoveAll(report => report.Target.ClientId == targetClient.ClientId);
            }

            if (activeClient.IsIngame)
            {
                ServerLogger.LogDebug("Attempting to kicking newly banned client {ActiveClient}", activeClient.ToString());
                
                var formattedString = string.Format(RconParser.Configuration.CommandPrefixes.Kick, 
                    activeClient.TemporalClientNumber, 
                    _messageFormatter.BuildFormattedMessage(RconParser.Configuration, newPenalty));
                await activeClient.CurrentServer.ExecuteCommandAsync(formattedString);
            }
            
            Manager.QueueEvent(new ClientPenaltyEvent
            {
                Client = targetClient,
                Penalty = newPenalty
            });
        }

        public override async Task Unban(string reason, EFClient targetClient, EFClient originClient)
        {
            var unbanPenalty = new EFPenalty
            {
                Type = EFPenalty.PenaltyType.Unban,
                Expires = DateTime.Now,
                Offender = targetClient,
                Offense = reason,
                Punisher = originClient,
                When = DateTime.UtcNow,
                Active = true,
                Link = targetClient.AliasLink
            };

            ServerLogger.LogDebug("Creating unban penalty for {targetClient}", targetClient.ToString());
            targetClient.SetLevel(Permission.User, originClient);
            await Manager.GetPenaltyService().RemoveActivePenalties(targetClient.AliasLink.AliasLinkId,
                targetClient.NetworkId, targetClient.GameName, targetClient.CurrentAlias?.IPAddress);
            await Manager.GetPenaltyService().Create(unbanPenalty);
            
            Manager.QueueEvent(new ClientPenaltyRevokeEvent
            {
                Client = targetClient,
                Penalty = unbanPenalty
            });
        }

        public override void InitializeTokens()
        {
            Manager.GetMessageTokens().Add(new MessageToken("TOTALPLAYERS", (Server s) => Task.Run(async () => (await Manager.GetClientService().GetTotalClientsAsync()).ToString())));
            Manager.GetMessageTokens().Add(new MessageToken("VERSION", (Server s) => Task.FromResult(Application.Program.Version.ToString())));
            Manager.GetMessageTokens().Add(new MessageToken("NEXTMAP", (Server s) => SharedLibraryCore.Commands.NextMapCommand.GetNextMap(s, _translationLookup)));
            Manager.GetMessageTokens().Add(new MessageToken("ADMINS", (Server s) => Task.FromResult(ListAdminsCommand.OnlineAdmins(s, _translationLookup))));
        }

        public override long LegacyDatabaseId => _cachedDatabaseServer.ServerId;
    }
}
