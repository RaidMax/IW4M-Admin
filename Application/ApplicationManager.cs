using IW4MAdmin.Application.API.Master;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Misc;
using IW4MAdmin.Application.RconParsers;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Configuration.Validation;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Events;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SharedLibraryCore.GameEvent;

namespace IW4MAdmin.Application
{
    public class ApplicationManager : IManager
    {
        private readonly ConcurrentBag<Server> _servers;
        public List<Server> Servers => _servers.OrderByDescending(s => s.ClientNum).ToList();
        public ILogger Logger => GetLogger(0);
        public bool Running { get; private set; }
        public bool IsInitialized { get; private set; }
        public DateTime StartTime { get; private set; }
        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        public IList<IRConParser> AdditionalRConParsers { get; }
        public IList<IEventParser> AdditionalEventParsers { get; }
        public ITokenAuthentication TokenAuthenticator { get; }
        public CancellationToken CancellationToken => _tokenSource.Token;
        public string ExternalIPAddress { get; private set; }
        public bool IsRestartRequested { get; private set; }
        public IMiddlewareActionHandler MiddlewareActionHandler { get; }
        private readonly List<Command> Commands;
        private readonly List<MessageToken> MessageTokens;
        private readonly ClientService ClientSvc;
        readonly AliasService AliasSvc;
        readonly PenaltyService PenaltySvc;
        public BaseConfigurationHandler<ApplicationConfiguration> ConfigHandler;
        GameEventHandler Handler;
        readonly IPageList PageList;
        private readonly Dictionary<long, ILogger> _loggers = new Dictionary<long, ILogger>();
        private readonly MetaService _metaService;
        private readonly TimeSpan _throttleTimeout = new TimeSpan(0, 1, 0);
        private readonly CancellationTokenSource _tokenSource;
        private readonly Dictionary<string, Task<IList>> _operationLookup = new Dictionary<string, Task<IList>>();

        public ApplicationManager(ILogger logger, IMiddlewareActionHandler actionHandler)
        {
            MiddlewareActionHandler = actionHandler;
            _servers = new ConcurrentBag<Server>();
            Commands = new List<Command>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = new ClientService();
            AliasSvc = new AliasService();
            PenaltySvc = new PenaltyService();
            ConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
            StartTime = DateTime.UtcNow;
            PageList = new PageList();
            AdditionalEventParsers = new List<IEventParser>();
            AdditionalRConParsers = new List<IRConParser>();
            TokenAuthenticator = new TokenAuthentication();
            _metaService = new MetaService();
            _tokenSource = new CancellationTokenSource();
            _loggers.Add(0, logger);
        }

        public async Task ExecuteEvent(GameEvent newEvent)
        {
#if DEBUG == true
            Logger.WriteDebug($"Entering event process for {newEvent.Id}");
#endif

            // the event has failed already
            if (newEvent.Failed)
            {
                goto skip;
            }

            try
            {
                await newEvent.Owner.ExecuteEvent(newEvent);

                // save the event info to the database
                var changeHistorySvc = new ChangeHistoryService();
                await changeHistorySvc.Add(newEvent);

#if DEBUG
                Logger.WriteDebug($"Processed event with id {newEvent.Id}");
#endif
            }

            catch (TaskCanceledException)
            {
                Logger.WriteInfo($"Received quit signal for event id {newEvent.Id}, so we are aborting early");
            }

            catch (OperationCanceledException)
            {
                Logger.WriteInfo($"Received quit signal for event id {newEvent.Id}, so we are aborting early");
            }

            // this happens if a plugin requires login
            catch (AuthorizationException ex)
            {
                newEvent.FailReason = EventFailReason.Permission;
                newEvent.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMAND_NOTAUTHORIZED"]} - {ex.Message}");
            }

            catch (NetworkException ex)
            {
                newEvent.FailReason = EventFailReason.Exception;
                Logger.WriteError(ex.Message);
                Logger.WriteDebug(ex.GetExceptionInfo());
            }

            catch (ServerException ex)
            {
                newEvent.FailReason = EventFailReason.Exception;
                Logger.WriteWarning(ex.Message);
            }

            catch (Exception ex)
            {
                newEvent.FailReason = EventFailReason.Exception;
                Logger.WriteError(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_EXCEPTION"].FormatExt(newEvent.Owner));
                Logger.WriteDebug(ex.GetExceptionInfo());
            }

        skip:
            // tell anyone waiting for the output that we're done
            newEvent.Complete();
#if DEBUG == true
            Logger.WriteDebug($"Exiting event process for {newEvent.Id}");
#endif
        }

        public IList<Server> GetServers()
        {
            return Servers;
        }

        public IList<Command> GetCommands()
        {
            return Commands;
        }

        public async Task UpdateServerStates()
        {
            // store the server hash code and task for it
            var runningUpdateTasks = new Dictionary<long, Task>();

            while (!_tokenSource.IsCancellationRequested)
            {
                // select the server ids that have completed the update task
                var serverTasksToRemove = runningUpdateTasks
                    .Where(ut => ut.Value.Status == TaskStatus.RanToCompletion ||
                        ut.Value.Status == TaskStatus.Canceled ||
                        ut.Value.Status == TaskStatus.Faulted)
                    .Select(ut => ut.Key)
                    .ToList();

                // this is to prevent the log reader from starting before the initial
                // query of players on the server
                if (serverTasksToRemove.Count > 0)
                {
                    IsInitialized = true;
                }

                // remove the update tasks as they have completd
                foreach (long serverId in serverTasksToRemove)
                {
                    runningUpdateTasks.Remove(serverId);
                }

                // select the servers where the tasks have completed
                var serverIds = Servers.Select(s => s.EndPoint).Except(runningUpdateTasks.Select(r => r.Key)).ToList();
                foreach (var server in Servers.Where(s => serverIds.Contains(s.EndPoint)))
                {
                    runningUpdateTasks.Add(server.EndPoint, Task.Run(async () =>
                    {
                        try
                        {
                            await server.ProcessUpdatesAsync(_tokenSource.Token);

                            if (server.Throttled)
                            {
                                await Task.Delay((int)_throttleTimeout.TotalMilliseconds, _tokenSource.Token);
                            }
                        }

                        catch (Exception e)
                        {
                            Logger.WriteWarning($"Failed to update status for {server}");
                            Logger.WriteDebug(e.GetExceptionInfo());
                        }

                        finally
                        {
                            server.IsInitialized = true;
                        }
                    }));
                }
#if DEBUG
                Logger.WriteDebug($"{runningUpdateTasks.Count} servers queued for stats updates");
                ThreadPool.GetMaxThreads(out int workerThreads, out int n);
                ThreadPool.GetAvailableThreads(out int availableThreads, out int m);
                Logger.WriteDebug($"There are {workerThreads - availableThreads} active threading tasks");
#endif
                try
                {
                    await Task.Delay(ConfigHandler.Configuration().RConPollRate, _tokenSource.Token);
                }
                // if a cancellation is received, we want to return immediately after shutting down
                catch
                {
                    foreach (var server in Servers.Where(s => serverIds.Contains(s.EndPoint)))
                    {
                        await server.ProcessUpdatesAsync(_tokenSource.Token);
                    }
                    break;
                }
            }
        }

        public async Task Init()
        {
            Running = true;
            ExternalIPAddress = await Utilities.GetExternalIP();

            #region PLUGINS
            SharedLibraryCore.Plugins.PluginImporter.Load(this);

            foreach (var Plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    await Plugin.OnLoadAsync(this);
                }

                catch (Exception ex)
                {
                    Logger.WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_PLUGIN"]} {Plugin.Name}");
                    Logger.WriteDebug(ex.GetExceptionInfo());
                }
            }
            #endregion

            #region CONFIG
            var config = ConfigHandler.Configuration();

            // copy over default config if it doesn't exist
            if (config == null)
            {
                var defaultConfig = new BaseConfigurationHandler<DefaultConfiguration>("DefaultSettings").Configuration();
                ConfigHandler.Set((ApplicationConfiguration)new ApplicationConfiguration().Generate());
                var newConfig = ConfigHandler.Configuration();

                newConfig.AutoMessages = defaultConfig.AutoMessages;
                newConfig.GlobalRules = defaultConfig.GlobalRules;
                newConfig.Maps = defaultConfig.Maps;
                newConfig.DisallowedClientNames = defaultConfig.DisallowedClientNames;
                newConfig.QuickMessages = defaultConfig.QuickMessages;

                if (newConfig.Servers == null)
                {
                    ConfigHandler.Set(newConfig);
                    newConfig.Servers = new ServerConfiguration[1];

                    do
                    {
                        var serverConfig = new ServerConfiguration();
                        foreach (var parser in AdditionalRConParsers)
                        {
                            serverConfig.AddRConParser(parser);
                        }

                        foreach (var parser in AdditionalEventParsers)
                        {
                            serverConfig.AddEventParser(parser);
                        }

                        newConfig.Servers[0] = (ServerConfiguration)serverConfig.Generate();
                    } while (Utilities.PromptBool(Utilities.CurrentLocalization.LocalizationIndex["SETUP_SERVER_SAVE"]));

                    config = newConfig;
                    await ConfigHandler.Save();
                }
            }

            else
            {
                if (string.IsNullOrEmpty(config.Id))
                {
                    config.Id = Guid.NewGuid().ToString();
                    await ConfigHandler.Save();
                }

                if (string.IsNullOrEmpty(config.WebfrontBindUrl))
                {
                    config.WebfrontBindUrl = "http://0.0.0.0:1624";
                    await ConfigHandler.Save();
                }

                var validator = new ApplicationConfigurationValidator();
                var validationResult = validator.Validate(config);

                if (!validationResult.IsValid)
                {
                    throw new ConfigurationException(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_CONFIGURATION_ERROR"])
                    {
                        Errors = validationResult.Errors.Select(_error => _error.ErrorMessage).ToArray()
                    };
                }

                foreach (var serverConfig in config.Servers)
                {
                    Migration.ConfigurationMigration.ModifyLogPath020919(serverConfig);

                    if (serverConfig.RConParserVersion == null || serverConfig.EventParserVersion == null)
                    {
                        foreach (var parser in AdditionalRConParsers)
                        {
                            serverConfig.AddRConParser(parser);
                        }

                        foreach (var parser in AdditionalEventParsers)
                        {
                            serverConfig.AddEventParser(parser);
                        }

                        serverConfig.ModifyParsers();
                    }
                    await ConfigHandler.Save();
                }
            }

            if (config.Servers.Length == 0)
            {
                throw new ServerException("A server configuration in IW4MAdminSettings.json is invalid");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Utilities.EncodingType = Encoding.GetEncoding(!string.IsNullOrEmpty(config.CustomParserEncoding) ? config.CustomParserEncoding : "windows-1252");

            #endregion

            #region DATABASE
            using (var db = new DatabaseContext(GetApplicationSettings().Configuration()?.ConnectionString,
                GetApplicationSettings().Configuration()?.DatabaseProvider))
            {
                await new ContextSeed(db).Seed();
            }
            #endregion

            #region COMMANDS
            if (ClientSvc.GetOwners().Result.Count == 0)
            {
                Commands.Add(new COwner());
            }

            Commands.Add(new CQuit());
            Commands.Add(new CRestart());
            Commands.Add(new CKick());
            Commands.Add(new CSay());
            Commands.Add(new CTempBan());
            Commands.Add(new CBan());
            Commands.Add(new CWhoAmI());
            Commands.Add(new CList());
            Commands.Add(new CHelp());
            Commands.Add(new CFastRestart());
            Commands.Add(new CMapRotate());
            Commands.Add(new CSetLevel());
            Commands.Add(new CUsage());
            Commands.Add(new CUptime());
            Commands.Add(new CWarn());
            Commands.Add(new CWarnClear());
            Commands.Add(new CUnban());
            Commands.Add(new CListAdmins());
            Commands.Add(new CLoadMap());
            Commands.Add(new CFindPlayer());
            Commands.Add(new CListRules());
            Commands.Add(new CPrivateMessage());
            Commands.Add(new CFlag());
            Commands.Add(new CUnflag());
            Commands.Add(new CReport());
            Commands.Add(new CListReports());
            Commands.Add(new CListBanInfo());
            Commands.Add(new CListAlias());
            Commands.Add(new CExecuteRCON());
            Commands.Add(new CPlugins());
            Commands.Add(new CIP());
            Commands.Add(new CMask());
            Commands.Add(new CPruneAdmins());
            //Commands.Add(new CKillServer());
            Commands.Add(new CSetPassword());
            Commands.Add(new CPing());
            Commands.Add(new CSetGravatar());
            Commands.Add(new CNextMap());
            Commands.Add(new RequestTokenCommand());
            Commands.Add(new UnlinkClientCommand());

            foreach (Command C in SharedLibraryCore.Plugins.PluginImporter.ActiveCommands)
            {
                Commands.Add(C);
            }
            #endregion

            #region META
            async Task<List<ProfileMeta>> getProfileMeta(int clientId, int offset, int count, DateTime? startAt)
            {
                var metaList = new List<ProfileMeta>();

                // we don't want to return anything because it means we're trying to retrieve paged meta data
                if (count > 1)
                {
                    return metaList;
                }

                var lastMapMeta = await _metaService.GetPersistentMeta("LastMapPlayed", new EFClient() { ClientId = clientId });

                if (lastMapMeta != null)
                {
                    metaList.Add(new ProfileMeta()
                    {
                        Id = lastMapMeta.MetaId,
                        Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_LAST_MAP"],
                        Value = lastMapMeta.Value,
                        Show = true,
                        Type = ProfileMeta.MetaType.Information,
                    });
                }

                var lastServerMeta = await _metaService.GetPersistentMeta("LastServerPlayed", new EFClient() { ClientId = clientId });

                if (lastServerMeta != null)
                {
                    metaList.Add(new ProfileMeta()
                    {
                        Id = lastServerMeta.MetaId,
                        Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_LAST_SERVER"],
                        Value = lastServerMeta.Value,
                        Show = true,
                        Type = ProfileMeta.MetaType.Information
                    });
                }

                var client = await GetClientService().Get(clientId);

                metaList.Add(new ProfileMeta()
                {
                    Id = client.ClientId,
                    Key = $"{Utilities.CurrentLocalization.LocalizationIndex["GLOBAL_TIME_HOURS"]} {Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_PLAYER"]}",
                    Value = Math.Round(client.TotalConnectionTime / 3600.0, 1).ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Show = true,
                    Column = 1,
                    Order = 0,
                    Type = ProfileMeta.MetaType.Information
                });

                metaList.Add(new ProfileMeta()
                {
                    Id = client.ClientId,
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_FSEEN"],
                    Value = Utilities.GetTimePassed(client.FirstConnection, false),
                    Show = true,
                    Column = 1,
                    Order = 1,
                    Type = ProfileMeta.MetaType.Information
                });

                metaList.Add(new ProfileMeta()
                {
                    Id = client.ClientId,
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_LSEEN"],
                    Value = Utilities.GetTimePassed(client.LastConnection, false),
                    Show = true,
                    Column = 1,
                    Order = 2,
                    Type = ProfileMeta.MetaType.Information
                });

                metaList.Add(new ProfileMeta()
                {
                    Id = client.ClientId,
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_CONNECTIONS"],
                    Value = client.Connections.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                    Show = true,
                    Column = 1,
                    Order = 3,
                    Type = ProfileMeta.MetaType.Information
                });

                metaList.Add(new ProfileMeta()
                {
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_MASKED"],
                    Value = client.Masked ? Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_TRUE"] : Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_FALSE"],
                    Sensitive = true,
                    Column = 1,
                    Order = 4,
                    Type = ProfileMeta.MetaType.Information
                });

                return metaList;
            }

            async Task<List<ProfileMeta>> getPenaltyMeta(int clientId, int offset, int count, DateTime? startAt)
            {
                if (count <= 1)
                {
                    return new List<ProfileMeta>();
                }

                var penalties = await GetPenaltyService().GetClientPenaltyForMetaAsync(clientId, count, offset, startAt);

                return penalties.Select(_penalty => new ProfileMeta()
                {
                    Id = _penalty.Id,
                    Type = _penalty.PunisherId == clientId ? ProfileMeta.MetaType.Penalized : ProfileMeta.MetaType.ReceivedPenalty,
                    Value = _penalty,
                    When = _penalty.TimePunished,
                    Sensitive = _penalty.Sensitive
                })
                .ToList();
            }

            MetaService.AddRuntimeMeta(getProfileMeta);
            MetaService.AddRuntimeMeta(getPenaltyMeta);
            #endregion

            await InitializeServers();
        }

        private async Task InitializeServers()
        {
            var config = ConfigHandler.Configuration();
            int successServers = 0;
            Exception lastException = null;

            async Task Init(ServerConfiguration Conf)
            {
                // setup the event handler after the class is initialized
                Handler = new GameEventHandler(this);

                try
                {
                    var ServerInstance = new IW4MServer(this, Conf);
                    await ServerInstance.Initialize();

                    _servers.Add(ServerInstance);

                    Logger.WriteVerbose(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_MONITORING_TEXT"].FormatExt(ServerInstance.Hostname));
                    // add the start event for this server

                    var e = new GameEvent()
                    {
                        Type = GameEvent.EventType.Start,
                        Data = $"{ServerInstance.GameName} started",
                        Owner = ServerInstance
                    };

                    Handler.AddEvent(e);
                    successServers++;
                }

                catch (ServerException e)
                {
                    Logger.WriteError(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_UNFIXABLE"].FormatExt($"[{Conf.IPAddress}:{Conf.Port}]"));

                    if (e.GetType() == typeof(DvarException))
                    {
                        Logger.WriteDebug($"{e.Message} {(e.GetType() == typeof(DvarException) ? $"({Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_DVAR_HELP"]})" : "")}");
                    }

                    lastException = e;
                }
            }

            await Task.WhenAll(config.Servers.Select(c => Init(c)).ToArray());

            if (successServers == 0)
            {
                throw lastException;
            }

            if (successServers != config.Servers.Length)
            {
                if (!Utilities.PromptBool(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_START_WITH_ERRORS"]))
                {
                    throw lastException;
                }
            }
        }

        private async Task SendHeartbeat()
        {
            bool connected = false;

            while (!_tokenSource.IsCancellationRequested)
            {
                if (!connected)
                {
                    try
                    {
                        await Heartbeat.Send(this, true);
                        connected = true;
                    }

                    catch (Exception e)
                    {
                        connected = false;
                        Logger.WriteWarning($"Could not connect to heartbeat server - {e.Message}");
                    }
                }

                else
                {
                    try
                    {
                        await Heartbeat.Send(this);
                    }

                    catch (System.Net.Http.HttpRequestException e)
                    {
                        Logger.WriteWarning($"Could not send heartbeat - {e.Message}");
                    }

                    catch (AggregateException e)
                    {
                        Logger.WriteWarning($"Could not send heartbeat - {e.Message}");
                        var exceptions = e.InnerExceptions.Where(ex => ex.GetType() == typeof(RestEase.ApiException));

                        foreach (var ex in exceptions)
                        {
                            if (((RestEase.ApiException)ex).StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            {
                                connected = false;
                            }
                        }
                    }

                    catch (RestEase.ApiException e)
                    {
                        Logger.WriteWarning($"Could not send heartbeat - {e.Message}");
                        if (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            connected = false;
                        }
                    }

                    catch (Exception e)
                    {
                        Logger.WriteWarning($"Could not send heartbeat - {e.Message}");
                    }

                }

                try
                {
                    await Task.Delay(30000, _tokenSource.Token);
                }
                catch { break; }
            }
        }

        public async Task Start()
        {
            await Task.WhenAll(new[]
            {
                SendHeartbeat(),
                UpdateServerStates()
            });
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            Running = false;
        }

        public void Restart()
        {
            IsRestartRequested = true;
            Stop();
        }

        public ILogger GetLogger(long serverId)
        {
            if (_loggers.ContainsKey(serverId))
            {
                return _loggers[serverId];
            }

            else
            {
                var newLogger = new Logger($"IW4MAdmin-Server-{serverId}");

                _loggers.Add(serverId, newLogger);
                return newLogger;
            }
        }

        public IList<MessageToken> GetMessageTokens()
        {
            return MessageTokens;
        }

        public IList<EFClient> GetActiveClients()
        {
            // we're adding another to list here so we don't get a collection modified exception..
            return _servers.SelectMany(s => s.Clients).ToList().Where(p => p != null).ToList();
        }

        public ClientService GetClientService()
        {
            return ClientSvc;
        }

        public AliasService GetAliasService()
        {
            return AliasSvc;
        }

        public PenaltyService GetPenaltyService()
        {
            return PenaltySvc;
        }

        public IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings()
        {
            return ConfigHandler;
        }

        public IEventHandler GetEventHandler()
        {
            return Handler;
        }

        public IList<Assembly> GetPluginAssemblies()
        {
            return SharedLibraryCore.Plugins.PluginImporter.PluginAssemblies.Union(SharedLibraryCore.Plugins.PluginImporter.Assemblies).ToList();
        }

        public IPageList GetPageList()
        {
            return PageList;
        }

        public IRConParser GenerateDynamicRConParser(string name)
        {
            return new DynamicRConParser()
            {
                Name = name
            };
        }

        public IEventParser GenerateDynamicEventParser(string name)
        {
            return new DynamicEventParser()
            {
                Name = name
            };
        }

        public async Task<IList<T>> ExecuteSharedDatabaseOperation<T>(string operationName)
        {
            var result = await _operationLookup[operationName];
            return (IList<T>)result;
        }

        public void RegisterSharedDatabaseOperation(Task<IList> operation, string operationName)
        {
            _operationLookup.Add(operationName, operation);
        }
    }
}
