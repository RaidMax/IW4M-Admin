using IW4MAdmin.Application.API.Master;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.RconParsers;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    public class ApplicationManager : IManager
    {
        private List<Server> _servers;
        public List<Server> Servers => _servers.OrderByDescending(s => s.ClientNum).ToList();
        public Dictionary<int, EFClient> PrivilegedClients { get; set; }
        public ILogger Logger => GetLogger(0);
        public bool Running { get; private set; }
        public bool IsInitialized { get; private set; }
        // define what the delagate function looks like
        public delegate void OnServerEventEventHandler(object sender, GameEventArgs e);
        // expose the event handler so we can execute the events
        public OnServerEventEventHandler OnServerEvent { get; set; }
        public DateTime StartTime { get; private set; }
        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        public IList<IRConParser> AdditionalRConParsers => _additionalRConParsers.ToList<IRConParser>();

        public IList<IEventParser> AdditionalEventParsers => _additionalEventParsers.ToList<IEventParser>();

        private readonly IList<DynamicRConParser> _additionalRConParsers;
        private readonly IList<DynamicEventParser> _additionalEventParsers;

        static ApplicationManager Instance;
        readonly List<AsyncStatus> TaskStatuses;
        List<Command> Commands;
        readonly List<MessageToken> MessageTokens;
        ClientService ClientSvc;
        readonly AliasService AliasSvc;
        readonly PenaltyService PenaltySvc;
        public BaseConfigurationHandler<ApplicationConfiguration> ConfigHandler;
        GameEventHandler Handler;
        ManualResetEventSlim OnQuit;
        readonly IPageList PageList;
        readonly SemaphoreSlim ProcessingEvent = new SemaphoreSlim(1, 1);
        readonly Dictionary<long, ILogger> Loggers = new Dictionary<long, ILogger>();

        private ApplicationManager()
        {
            _servers = new List<Server>();
            Commands = new List<Command>();
            TaskStatuses = new List<AsyncStatus>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = new ClientService();
            AliasSvc = new AliasService();
            PenaltySvc = new PenaltyService();
            ConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
            StartTime = DateTime.UtcNow;
            OnQuit = new ManualResetEventSlim();
            PageList = new PageList();
            _additionalEventParsers = new List<DynamicEventParser>();
            _additionalRConParsers = new List<DynamicRConParser>();
            OnServerEvent += OnGameEvent;
            OnServerEvent += EventApi.OnGameEvent;
        }

        private async void OnGameEvent(object sender, GameEventArgs args)
        {
#if DEBUG == true
            Logger.WriteDebug($"Entering event process for {args.Event.Id}");
#endif

            var newEvent = args.Event;

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
                await changeHistorySvc.Add(args.Event);

#if DEBUG
                Logger.WriteDebug($"Processed event with id {newEvent.Id}");
#endif
            }

            // this happens if a plugin requires login
            catch (AuthorizationException ex)
            {
                newEvent.FailReason = GameEvent.EventFailReason.Permission;
                newEvent.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMAND_NOTAUTHORIZED"]} - {ex.Message}");
            }

            catch (NetworkException ex)
            {
                newEvent.FailReason = GameEvent.EventFailReason.Exception;
                Logger.WriteError(ex.Message);
                Logger.WriteDebug(ex.GetExceptionInfo());
            }

            catch (ServerException ex)
            {
                newEvent.FailReason = GameEvent.EventFailReason.Exception;
                Logger.WriteWarning(ex.Message);
            }

            catch (Exception ex)
            {
                newEvent.FailReason = GameEvent.EventFailReason.Exception;
                Logger.WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_EXCEPTION"]} {newEvent.Owner}");
                Logger.WriteDebug(ex.GetExceptionInfo());
            }

        skip:

            // tell anyone waiting for the output that we're done
            newEvent.OnProcessed.Set();
        }

        public IList<Server> GetServers()
        {
            return Servers;
        }

        public IList<Command> GetCommands()
        {
            return Commands;
        }

        public static ApplicationManager GetInstance()
        {
            return Instance ?? (Instance = new ApplicationManager());
        }

        public async Task UpdateServerStates()
        {
            // store the server hash code and task for it
            var runningUpdateTasks = new Dictionary<long, Task>();

            while (Running)
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
                            await server.ProcessUpdatesAsync(new CancellationToken());
                        }

                        catch (Exception e)
                        {
                            Logger.WriteWarning($"Failed to update status for {server}");
                            Logger.WriteDebug(e.GetExceptionInfo());
                        }
                    }));
                }
#if DEBUG
                Logger.WriteDebug($"{runningUpdateTasks.Count} servers queued for stats updates");
                ThreadPool.GetMaxThreads(out int workerThreads, out int n);
                ThreadPool.GetAvailableThreads(out int availableThreads, out int m);
                Logger.WriteDebug($"There are {workerThreads - availableThreads} active threading tasks");
#endif
                await Task.Delay(ConfigHandler.Configuration().RConPollRate);
            }

            // trigger the event processing loop to end
            SetHasEvent();
        }

        public async Task Init()
        {
            Running = true;

            #region CONFIG
            var config = ConfigHandler.Configuration();

            // copy over default config if it doesn't exist
            if (config == null)
            {
                var defaultConfig = new BaseConfigurationHandler<DefaultConfiguration>("DefaultSettings").Configuration();
                ConfigHandler.Set((ApplicationConfiguration)new ApplicationConfiguration().Generate());
                var newConfig = ConfigHandler.Configuration();

                newConfig.AutoMessagePeriod = defaultConfig.AutoMessagePeriod;
                newConfig.AutoMessages = defaultConfig.AutoMessages;
                newConfig.GlobalRules = defaultConfig.GlobalRules;
                newConfig.Maps = defaultConfig.Maps;

                if (newConfig.Servers == null)
                {
                    ConfigHandler.Set(newConfig);
                    newConfig.Servers = new List<ServerConfiguration>();

                    do
                    {
                        newConfig.Servers.Add((ServerConfiguration)new ServerConfiguration().Generate());
                    } while (Utilities.PromptBool(Utilities.CurrentLocalization.LocalizationIndex["SETUP_SERVER_SAVE"]));

                    config = newConfig;
                    await ConfigHandler.Save();
                }
            }

            else if (config != null)
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
            }

            else if (config.Servers.Count == 0)
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

            PrivilegedClients = (await ClientSvc.GetPrivilegedClients()).ToDictionary(_client => _client.ClientId);
            #endregion

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

            #region COMMANDS
            if (ClientSvc.GetOwners().Result.Count == 0)
            {
                Commands.Add(new COwner());
            }

            Commands.Add(new CQuit());
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
            Commands.Add(new CKillServer());
            Commands.Add(new CSetPassword());
            Commands.Add(new CPing());
            Commands.Add(new CSetGravatar());
            Commands.Add(new CNextMap());

            foreach (Command C in SharedLibraryCore.Plugins.PluginImporter.ActiveCommands)
            {
                Commands.Add(C);
            }
            #endregion

            #region INIT
            async Task Init(ServerConfiguration Conf)
            {
                // setup the event handler after the class is initialized
                Handler = new GameEventHandler(this);
                try
                {
                    var ServerInstance = new IW4MServer(this, Conf);
                    await ServerInstance.Initialize();

                    lock (_servers)
                    {
                        _servers.Add(ServerInstance);
                    }

                    Logger.WriteVerbose($"{Utilities.CurrentLocalization.LocalizationIndex["MANAGER_MONITORING_TEXT"]} {ServerInstance.Hostname}");
                    // add the start event for this server

                    var e = new GameEvent()
                    {
                        Type = GameEvent.EventType.Start,
                        Data = $"{ServerInstance.GameName} started",
                        Owner = ServerInstance
                    };

                    Handler.AddEvent(e);
                }

                catch (ServerException e)
                {
                    Logger.WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_UNFIXABLE"]} [{Conf.IPAddress}:{Conf.Port}]");
                    if (e.GetType() == typeof(DvarException))
                    {
                        Logger.WriteDebug($"{Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_DVAR"]} {(e as DvarException).Data["dvar_name"]} ({Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_DVAR_HELP"]})");
                    }
                    else if (e.GetType() == typeof(NetworkException))
                    {
                        Logger.WriteDebug(e.Message);
                    }

                    // throw the exception to the main method to stop before instantly exiting
                    throw e;
                }
            }

            await Task.WhenAll(config.Servers.Select(c => Init(c)).ToArray());
            #endregion
        }

        private async Task SendHeartbeat(object state)
        {
            var heartbeatState = (HeartbeatState)state;

            while (Running)
            {
                if (!heartbeatState.Connected)
                {
                    try
                    {
                        await Heartbeat.Send(this, true);
                        heartbeatState.Connected = true;
                    }

                    catch (Exception e)
                    {
                        heartbeatState.Connected = false;
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
                                heartbeatState.Connected = false;
                            }
                        }
                    }

                    catch (RestEase.ApiException e)
                    {
                        Logger.WriteWarning($"Could not send heartbeat - {e.Message}");
                        if (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            heartbeatState.Connected = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.WriteWarning($"Could not send heartbeat - {e.Message}");
                    }

                }
                await Task.Delay(30000);
            }
        }

        public void Start()
        {
            // this needs to be run seperately from the main thread
            var _ = Task.Run(() => SendHeartbeat(new HeartbeatState()));
            _ = Task.Run(() => UpdateServerStates());

            while (Running)
            {
                OnQuit.Wait();
                OnQuit.Reset();
            }
            _servers.Clear();
        }

        public void Stop()
        {
            Running = false;
        }

        public ILogger GetLogger(long serverId)
        {
            if (Loggers.ContainsKey(serverId))
            {
                return Loggers[serverId];
            }

            else
            {
                Logger newLogger;

                if (serverId == 0)
                {
                    newLogger = new Logger("IW4MAdmin-Manager");
                }
                else
                {
                    newLogger = new Logger($"IW4MAdmin-Server-{serverId}");
                }

                Loggers.Add(serverId, newLogger);
                return newLogger;
            }
        }

        public IList<MessageToken> GetMessageTokens()
        {
            return MessageTokens;
        }

        public IList<EFClient> GetActiveClients()
        {
            return _servers.SelectMany(s => s.Clients).Where(p => p != null).ToList();
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

        public IDictionary<int, EFClient> GetPrivilegedClients()
        {
            return PrivilegedClients;
        }

        public bool ShutdownRequested()
        {
            return !Running;
        }

        public IEventHandler GetEventHandler()
        {
            return Handler;
        }

        public void SetHasEvent()
        {
            OnQuit.Set();
        }

        public IList<Assembly> GetPluginAssemblies()
        {
            return SharedLibraryCore.Plugins.PluginImporter.PluginAssemblies;
        }

        public IPageList GetPageList()
        {
            return PageList;
        }

        public IRConParser GenerateDynamicRConParser()
        {
            return new DynamicRConParser();
        }

        public IEventParser GenerateDynamicEventParser()
        {
            return new DynamicEventParser();
        }
    }
}
