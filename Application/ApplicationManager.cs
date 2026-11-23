using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Misc;
using IW4MAdmin.Application.RConParsers;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Configuration.Validation;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Context;
using Data.Models;
using IW4MAdmin.Application.Configuration;
using IW4MAdmin.Application.IO;
using IW4MAdmin.Application.Migration;
using IW4MAdmin.Application.Plugin.Script;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Events;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Formatting;
using SharedLibraryCore.Interfaces.Events;
using static SharedLibraryCore.GameEvent;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using ObsoleteLogger = SharedLibraryCore.Interfaces.ILogger;

namespace IW4MAdmin.Application
{
    public class ApplicationManager : IManager
    {
        private readonly ConcurrentDictionary<string, Server> _servers;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _serverCancellationTokens;
        public List<Server> Servers => _servers.Values.OrderByDescending(s => s.ClientNum).ToList();
        [Obsolete] public ObsoleteLogger Logger => _serviceProvider.GetRequiredService<ObsoleteLogger>();
        public bool IsRunning { get; private set; }
        public bool IsInitialized { get; private set; }
        public DateTime StartTime { get; private set; }
        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        // base parsers are owned by the manager; .cs parser definitions are owned by the
        // CsPluginServiceHost and projected here (resolved lazily to avoid a DI cycle:
        // manager -> host -> CsPluginCommandRegistrar -> IManager).
        private readonly IRConParser _baseRConParser;
        private readonly IEventParser _baseEventParser;

        public IReadOnlyList<IRConParser> AdditionalRConParsers =>
            _serviceProvider.GetRequiredService<ICsPluginServiceHost>().LoadedRConParsers
                .Prepend(_baseRConParser).ToList();

        public IReadOnlyList<IEventParser> AdditionalEventParsers =>
            _serviceProvider.GetRequiredService<ICsPluginServiceHost>().LoadedEventParsers
                .Prepend(_baseEventParser).ToList();
        public IList<Func<GameEvent, bool>> CommandInterceptors { get; set; } =
            new List<Func<GameEvent, bool>>();
        public ITokenAuthentication TokenAuthenticator { get; }
        public CancellationToken CancellationToken => _isRunningTokenSource.Token;
        public string ExternalIPAddress { get; private set; }
        public bool IsRestartRequested { get; private set; }
        public IMiddlewareActionHandler MiddlewareActionHandler { get; }
        public event EventHandler<GameEvent> OnGameEventExecuted;
        private readonly List<IManagerCommand> _commands;
        private readonly ILogger _logger;
        private readonly List<MessageToken> MessageTokens;
        private readonly ClientService ClientSvc;
        readonly PenaltyService PenaltySvc;
        private readonly IAlertManager _alertManager;
        private readonly ConfigurationWatcher _watcher;
        public readonly IConfigurationHandlerV2<ApplicationConfiguration> ConfigHandler;
        private readonly IConfigurationHandler<ApplicationConfiguration> _legacyConfigHandler;
        readonly IPageList PageList;
        private readonly TimeSpan _throttleTimeout = new TimeSpan(0, 1, 0);
        private CancellationTokenSource _isRunningTokenSource;
        private CancellationTokenSource _eventHandlerTokenSource;
        private readonly Dictionary<string, Task<IList>> _operationLookup = new Dictionary<string, Task<IList>>();
        private readonly ITranslationLookup _translationLookup;
        private readonly IConfigurationHandler<CommandConfiguration> _commandConfiguration;
        private readonly IGameServerInstanceFactory _serverInstanceFactory;
        private readonly IParserRegexFactory _parserRegexFactory;
        private readonly IEnumerable<IRegisterEvent> _customParserEvents;
        private readonly ICoreEventHandler _coreEventHandler;
        private readonly IScriptCommandFactory _scriptCommandFactory;
        private readonly IMetaRegistration _metaRegistration;
        private readonly IScriptPluginServiceResolver _scriptPluginServiceResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly ChangeHistoryService _changeHistoryService;
        private readonly ApplicationConfiguration _appConfig;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
        private volatile ApplicationConfiguration _pendingSyncConfig;
        public ConcurrentDictionary<long, GameEvent> ProcessingEvents { get; } = new();

        public ApplicationManager(ILogger<ApplicationManager> logger, IMiddlewareActionHandler actionHandler, IEnumerable<IManagerCommand> commands,
            ITranslationLookup translationLookup, IConfigurationHandler<CommandConfiguration> commandConfiguration,
            IConfigurationHandlerV2<ApplicationConfiguration> appConfigHandler,
            IConfigurationHandler<ApplicationConfiguration> legacyConfigHandler, IGameServerInstanceFactory serverInstanceFactory,
            IEnumerable<IPlugin> plugins, IParserRegexFactory parserRegexFactory, IEnumerable<IRegisterEvent> customParserEvents,
            ICoreEventHandler coreEventHandler, IScriptCommandFactory scriptCommandFactory, IDatabaseContextFactory contextFactory,
            IMetaRegistration metaRegistration, IScriptPluginServiceResolver scriptPluginServiceResolver, ClientService clientService, IServiceProvider serviceProvider,
            ChangeHistoryService changeHistoryService, ApplicationConfiguration appConfig, PenaltyService penaltyService, IAlertManager alertManager, IInteractionRegistration interactionRegistration, IEnumerable<IPluginV2> v2PLugins,
            ConfigurationWatcher watcher)
        {
            MiddlewareActionHandler = actionHandler;
            _servers = new ConcurrentDictionary<string, Server>();
            _serverCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = clientService;
            PenaltySvc = penaltyService;
            _alertManager = alertManager;
            _watcher = watcher;
            ConfigHandler = appConfigHandler;
            _legacyConfigHandler = legacyConfigHandler;
            StartTime = DateTime.UtcNow;
            PageList = new PageList();
            _baseEventParser = new BaseEventParser(parserRegexFactory, logger, appConfig, serviceProvider.GetRequiredService<IGameScriptEventFactory>());
            _baseRConParser = new BaseRConParser(serviceProvider.GetRequiredService<ILogger<BaseRConParser>>(), parserRegexFactory);
            TokenAuthenticator = new TokenAuthentication();
            _logger = logger;
            _isRunningTokenSource = new CancellationTokenSource();
            _commands = commands.ToList();
            _translationLookup = translationLookup;
            _commandConfiguration = commandConfiguration;
            _serverInstanceFactory = serverInstanceFactory;
            _parserRegexFactory = parserRegexFactory;
            _customParserEvents = customParserEvents;
            _coreEventHandler = coreEventHandler;
            _scriptCommandFactory = scriptCommandFactory;
            _metaRegistration = metaRegistration;
            _scriptPluginServiceResolver = scriptPluginServiceResolver;
            _serviceProvider = serviceProvider;
            _changeHistoryService = changeHistoryService;
            _appConfig = appConfig;
            Plugins = plugins;
            InteractionRegistration = interactionRegistration;
            
            IManagementEventSubscriptions.ClientPersistentIdReceived += OnClientPersistentIdReceived;
        }

        public IEnumerable<IPlugin> Plugins { get; }
        public IInteractionRegistration InteractionRegistration { get; }

        public async Task ExecuteEvent(GameEvent newEvent)
        {
            ProcessingEvents.TryAdd(newEvent.IncrementalId, newEvent);
            
            // the event has failed already
            if (newEvent.Failed)
            {
                goto skip;
            }

            try
            {
                await newEvent.Owner.ExecuteEvent(newEvent);

                // save the event info to the database
                await _changeHistoryService.Add(newEvent);
            }

            catch (TaskCanceledException)
            {
                _logger.LogDebug("Received quit signal for event id {EventId}, so we are aborting early", newEvent.IncrementalId);
            }

            catch (OperationCanceledException)
            {
                _logger.LogDebug("Received quit signal for event id {EventId}, so we are aborting early", newEvent.IncrementalId);
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
                using (LogContext.PushProperty("Server", newEvent.Owner?.ToString()))
                {
                    _logger.LogError(ex, ex.Message);
                }
            }

            catch (ServerException ex)
            {
                newEvent.FailReason = EventFailReason.Exception;
                using (LogContext.PushProperty("Server", newEvent.Owner?.ToString()))
                {
                    _logger.LogError(ex, ex.Message);
                }
            }

            catch (Exception ex)
            {
                newEvent.FailReason = EventFailReason.Exception;
                Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_EXCEPTION"].FormatExt(newEvent.Owner));
                using (LogContext.PushProperty("Server", newEvent.Owner?.ToString()))
                {
                    _logger.LogError(ex, "Unexpected exception");
                }
            }

            skip:
            if (newEvent.Type == EventType.Command && newEvent.ImpersonationOrigin == null && newEvent.CorrelationId is not null)
            {
                var correlatedEvents =
                    ProcessingEvents.Values.Where(ev =>
                            ev.CorrelationId == newEvent.CorrelationId && ev.IncrementalId != newEvent.IncrementalId)
                        .ToList();

                await Task.WhenAll(correlatedEvents.Select(ev =>
                    ev.WaitAsync(Utilities.DefaultCommandTimeout, CancellationToken)));
                newEvent.Output.AddRange(correlatedEvents.SelectMany(ev => ev.Output));

                foreach (var correlatedEvent in correlatedEvents)
                {
                    ProcessingEvents.Remove(correlatedEvent.IncrementalId, out _);
                }
            }

            // we don't want to remove events that are correlated to command
            if (ProcessingEvents.Values.Count(gameEvent =>
                    newEvent.CorrelationId is not null && newEvent.CorrelationId == gameEvent.CorrelationId) == 1 ||
                newEvent.CorrelationId is null)
            {
                ProcessingEvents.Remove(newEvent.IncrementalId, out _);
            }

            // tell anyone waiting for the output that we're done
            newEvent.Complete();
            OnGameEventExecuted?.Invoke(this, newEvent);
        }

        public IList<Server> GetServers()
        {
            return Servers
                .Where(server => server is not DummyServer)
                .ToList();
        }

        public IReadOnlyList<IManagerCommand> Commands
        {
            get 
            {
                lock (_commands)
                {
                    return _commands.ToImmutableList();
                } 
            }
        }

        private Task UpdateServerStates()
        {
            var index = 0;
            var serverTasks = _servers.Values.Select(server =>
            {
                var thisIndex = index;
                Interlocked.Increment(ref index);
                return ProcessUpdateHandler(server, thisIndex);
            }).ToList();

            // Prevent Task.WhenAll from completing when all servers are dynamically removed,
            // which would cause Start() to kill the event handler prematurely.
            serverTasks.Add(Task.Delay(Timeout.Infinite, _isRunningTokenSource.Token));

            return Task.WhenAll(serverTasks);
        }

        private async Task ProcessUpdateHandler(Server server, int index)
        {
            const int delayScalar = 50; // Task.Delay is inconsistent enough there's no reason to try to prevent collisions
            var timeout = TimeSpan.FromMinutes(2);
            var serverKey = server.Id;
            
            // Get the per-server cancellation token, or create one if it doesn't exist
            if (!_serverCancellationTokens.TryGetValue(serverKey, out var serverTokenSource))
            {
                serverTokenSource = new CancellationTokenSource();
                _serverCancellationTokens[serverKey] = serverTokenSource;
            }

            while (!_isRunningTokenSource.IsCancellationRequested && !serverTokenSource.IsCancellationRequested)
            {
                try
                {
                    var delayFactor = Math.Min(_appConfig.RConPollRate, delayScalar * index);
                    using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                        _isRunningTokenSource.Token, serverTokenSource.Token);
                    await Task.Delay(delayFactor, combinedTokenSource.Token);

                    using var timeoutTokenSource = new CancellationTokenSource();
                    timeoutTokenSource.CancelAfter(timeout);
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token,
                            _isRunningTokenSource.Token, serverTokenSource.Token);
                    await server.ProcessUpdatesAsync(linkedTokenSource.Token);

                    await Task.Delay(Math.Max(1000, _appConfig.RConPollRate - delayFactor),
                        combinedTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // ignored - either the whole app is stopping or this specific server was removed
                }
                catch (Exception ex)
                {
                    using (LogContext.PushProperty("Server", server.Id))
                    {
                        _logger.LogError(ex, "Failed to update status");
                    }
                }
                finally
                {
                    server.IsInitialized = true;
                }
            }
            
            // run the final updates to clean up server
            await server.ProcessUpdatesAsync(CancellationToken.None);
        }

        public async Task Init()
        {
            IsRunning = true;
            ExternalIPAddress = await Utilities.GetExternalIP();

            #region DATABASE
            _logger.LogInformation("Beginning database migration sync");
            Console.WriteLine(_translationLookup["MANAGER_MIGRATION_START"]);
            await ContextSeed.Seed(_serviceProvider.GetRequiredService<IDatabaseContextFactory>(), _isRunningTokenSource.Token);
            await DatabaseHousekeeping.RemoveOldRatings(_serviceProvider.GetRequiredService<IDatabaseContextFactory>(), _isRunningTokenSource.Token);
            _logger.LogInformation("Finished database migration sync");
            Console.WriteLine(_translationLookup["MANAGER_MIGRATION_END"]);
            #endregion
            
            #region EVENTS                        
            IGameServerEventSubscriptions.ServerValueRequested += OnServerValueRequested;
            IGameServerEventSubscriptions.ServerValueSetRequested += OnServerValueSetRequested;
            IGameServerEventSubscriptions.ServerCommandExecuteRequested += OnServerCommandExecuteRequested;
            await IManagementEventSubscriptions.InvokeLoadAsync(this, CancellationToken);
            # endregion

            #region PLUGINS
            foreach (var plugin in Plugins)
            {
                try
                {
                    if (plugin is ScriptPlugin scriptPlugin && !plugin.IsParser)
                    {
                        await scriptPlugin.Initialize(this, _scriptCommandFactory, _scriptPluginServiceResolver, 
                            _serviceProvider.GetService<IConfigurationHandlerV2<ScriptPluginConfiguration>>());
                        scriptPlugin.Watcher.Changed += async (sender, e) =>
                        {
                            try
                            {
                                await scriptPlugin.Initialize(this, _scriptCommandFactory, _scriptPluginServiceResolver, 
                                    _serviceProvider.GetService<IConfigurationHandlerV2<ScriptPluginConfiguration>>());
                            }

                            catch (Exception ex)
                            {
                                Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_ERROR"].FormatExt(scriptPlugin.Name));
                                _logger.LogError(ex, "Could not properly load plugin {plugin}", scriptPlugin.Name);
                            }
                        };
                    }

                    else
                    {
                        await plugin.OnLoadAsync(this);
                    }
                }

                catch (Exception ex) when (PluginApiCompatibility.IsMissingApiException(ex))
                {
                    PluginApiCompatibility.NotifyNewerApiRequired(plugin.GetType().Assembly, plugin.Name);
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_translationLookup["SERVER_ERROR_PLUGIN"]} {plugin.Name}");
                }
            }
            #endregion

            #region CONFIG
            // copy over default config if it doesn't exist
            // Only run setup wizard if this is a fresh config (no Id set), not when servers are intentionally empty
            var isFirstRun = string.IsNullOrEmpty(_appConfig.Id);
            if (isFirstRun && _appConfig.Servers is null)
            {
                var defaultHandler = new BaseConfigurationHandler<DefaultSettings>("DefaultSettings");
                await defaultHandler.BuildAsync();
                var defaultConfig = defaultHandler.Configuration();
        
                _appConfig.AutoMessages = defaultConfig.AutoMessages;
                _appConfig.GlobalRules = defaultConfig.GlobalRules;
                _appConfig.DisallowedClientNames = defaultConfig.DisallowedClientNames;

                //if (newConfig.Servers == null)
                {
                    await ConfigHandler.Set(_appConfig);
                    _appConfig.Servers = new ServerConfiguration[1];

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

                        _appConfig.Servers = _appConfig.Servers.Where(_servers => _servers != null).Append((ServerConfiguration)serverConfig.Generate()).ToArray();
                    } while (Utilities.PromptBool(_translationLookup["SETUP_SERVER_SAVE"]));

                    await ConfigHandler.Set(_appConfig);
                }
            }

            else
            {
                if (string.IsNullOrEmpty(_appConfig.Id))
                {
                    _appConfig.Id = Guid.NewGuid().ToString();
                }

                if (string.IsNullOrEmpty(_appConfig.Webfront.BindUrl))
                {
                    _appConfig.Webfront.BindUrl = "http://0.0.0.0:1624";
                }

#pragma warning disable 618
                // Migrate obsolete Webfront properties to Webfront.* properties
                if (_appConfig.EnableWebFront.HasValue)
                {
                    _appConfig.Webfront.Enabled = _appConfig.EnableWebFront.Value;
                    _appConfig.EnableWebFront = null;
                }

                if (_appConfig.WebfrontBindUrl != null)
                {
                    _appConfig.Webfront.BindUrl = _appConfig.WebfrontBindUrl;
                    _appConfig.WebfrontBindUrl = null;
                }

                if (_appConfig.ManualWebfrontUrl != null)
                {
                    _appConfig.Webfront.ManualUrl = _appConfig.ManualWebfrontUrl;
                    _appConfig.ManualWebfrontUrl = null;
                }

                if (_appConfig.WebfrontCustomBranding != null)
                {
                    _appConfig.Webfront.CustomBranding = _appConfig.WebfrontCustomBranding;
                    _appConfig.WebfrontCustomBranding = null;
                }

                if (_appConfig.EnableWebfrontConnectionWhitelist.HasValue)
                {
                    _appConfig.Webfront.EnableConnectionWhitelist = _appConfig.EnableWebfrontConnectionWhitelist.Value;
                    _appConfig.EnableWebfrontConnectionWhitelist = null;
                }

                if (_appConfig.WebfrontConnectionWhitelist != null)
                {
                    _appConfig.Webfront.ConnectionWhitelist = _appConfig.WebfrontConnectionWhitelist;
                    _appConfig.WebfrontConnectionWhitelist = null;
                }

                if (_appConfig.WebfrontPrimaryColor != null)
                {
                    _appConfig.Webfront.PrimaryColor = _appConfig.WebfrontPrimaryColor;
                    _appConfig.WebfrontPrimaryColor = null;
                }

                if (_appConfig.WebfrontSecondaryColor != null)
                {
                    _appConfig.Webfront.SecondaryColor = _appConfig.WebfrontSecondaryColor;
                    _appConfig.WebfrontSecondaryColor = null;
                }
                
                // Migrate old PermissionSets to Webfront.PermissionSets
                if (_appConfig.PermissionSets is { Count: > 0 })
                {
                    // Copy all permission sets from old location to new location
                    foreach (var (level, permissions) in _appConfig.PermissionSets)
                    {
                        _appConfig.Webfront.PermissionSets[level] = permissions;
                    }
                    _appConfig.PermissionSets = null;
                }
                
                // Migrate old wildcard-only permission sets to new tiered defaults
                var defaultConfig = new ApplicationConfiguration();
                foreach (var (permissionLevel, permissions) in _appConfig.Webfront.PermissionSets.ToList())
                {
                    // Only migrate if using old grant-all default (single "*" entry)
                    if (permissions is ["*"] && defaultConfig.Webfront.PermissionSets.TryGetValue(permissionLevel, out var newDefault) && newDefault is not ["*"])
                    {
                        _appConfig.Webfront.PermissionSets[permissionLevel] = newDefault;
                    }
                }
                
                // Add User permission set if missing (new addition)
                if (!_appConfig.Webfront.PermissionSets.ContainsKey(Data.Models.Client.EFClient.Permission.User.ToString()) && 
                    defaultConfig.Webfront.PermissionSets.TryGetValue(Data.Models.Client.EFClient.Permission.User.ToString(), out var userPermissions))
                {
                    _appConfig.Webfront.PermissionSets[Data.Models.Client.EFClient.Permission.User.ToString()] = userPermissions;
                }
#pragma warning restore 618

                var validator = new ApplicationConfigurationValidator();
                var validationResult = validator.Validate(_appConfig);

                if (!validationResult.IsValid)
                {
                    throw new ConfigurationException("Could not validate configuration")
                    {
                        Errors = validationResult.Errors.Select(_error => _error.ErrorMessage).ToArray(),
                        ConfigurationFileName = ConfigHandler.Filename
                    };
                }

                foreach (var serverConfig in _appConfig.Servers)
                {
                    ConfigurationMigration.ModifyLogPath020919(serverConfig);
                    ConfigurationMigration.UpdatePlutoniumT6Parser(serverConfig);

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
                }
                
                await ConfigHandler.Set(_appConfig);
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Utilities.EncodingType = Encoding.GetEncoding(!string.IsNullOrEmpty(_appConfig.CustomParserEncoding) ? _appConfig.CustomParserEncoding : "windows-1252");

            foreach (var parser in AdditionalRConParsers)
            {
                if (!parser.Configuration.ColorCodeMapping.ContainsKey(ColorCodes.Accent.ToString()))
                {
                    parser.Configuration.ColorCodeMapping.Add(ColorCodes.Accent.ToString(),
                        parser.Configuration.ColorCodeMapping.TryGetValue(_appConfig.IngameAccentColorKey, out var colorCode)
                            ? colorCode
                            : "");
                }
            }

            #endregion

            #region COMMANDS
            if (await ClientSvc.HasOwnerAsync(_isRunningTokenSource.Token))
            {
                lock (_commands)
                {
                    _commands.RemoveAll(cmd => cmd.GetType() == typeof(OwnerCommand));
                }
            }

            List<IManagerCommand> commandsToAddToConfig = [];
            var cmdConfig = _commandConfiguration.Configuration();

            if (cmdConfig == null)
            {
                cmdConfig = new CommandConfiguration();
                commandsToAddToConfig.AddRange(Commands);
            }

            else
            {
                var unsavedCommands = Commands
                    .Where(cmd => !cmdConfig.Commands.ContainsKey(cmd.CommandConfigNameForType()));
                commandsToAddToConfig.AddRange(unsavedCommands);
            }

            // this is because I want to store the command prefix in IW4MAdminSettings, but can't easily
            // inject it to all the places that need it
            cmdConfig.CommandPrefix = _appConfig?.CommandPrefix ?? "!";
            cmdConfig.BroadcastCommandPrefix = _appConfig?.BroadcastCommandPrefix ?? "@";

            foreach (var cmd in commandsToAddToConfig)
            {
                if (cmdConfig.Commands.ContainsKey(cmd.CommandConfigNameForType()))
                {
                    continue;
                }
                cmdConfig.Commands.Add(cmd.CommandConfigNameForType(),
                new CommandProperties
                {
                    Name = cmd.Name,
                    Alias = cmd.Alias,
                    MinimumPermission = cmd.Permission,
                    AllowImpersonation = cmd.AllowImpersonation,
                    SupportedGames = cmd.SupportedGames
                });
            }

            _commandConfiguration.Set(cmdConfig);
            await _commandConfiguration.Save();
            #endregion

            _metaRegistration.Register();
            await _alertManager.Initialize();

            #region CUSTOM_EVENTS
            foreach (var customEvent in _customParserEvents.SelectMany(_events => _events.Events))
            {
                foreach (var parser in AdditionalEventParsers)
                {
                    parser.RegisterCustomEvent(customEvent.Item1, customEvent.Item2, customEvent.Item3);
                }
            }
            #endregion
            
            Console.WriteLine(_translationLookup["MANAGER_COMMUNICATION_INFO"]);
            await InitializeServers();

            ConfigHandler.Updated += SyncServersWithConfigurationAsync;
            _watcher.Enable();
            IsInitialized = true;
        }

        private async Task InitializeServers()
        {
            // Handle case where no servers are configured - create dummy server
            if (_appConfig.Servers == null || _appConfig.Servers.Length == 0)
            {
                var dummyServer = _serverInstanceFactory.CreateDummyServer(this) as DummyServer;
                await dummyServer!.Initialize();
                _servers[dummyServer.Id] = dummyServer;
                
                Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_NO_SERVERS_CONFIGURED"]);
                _logger.LogInformation("No servers configured. Running with dummy server");
                return;
            }

            var successServers = 0;
            Exception lastException = null;

            async Task InitializeEachServer(ServerConfiguration configuration)
            {
                try
                {
                    // todo: this might not always be an IW4MServer
                    var serverInstance = _serverInstanceFactory.CreateServer(configuration, this) as IW4MServer;
                    using (LogContext.PushProperty("Server", serverInstance!.ToString()))
                    {
                        _logger.LogInformation("Beginning server communication initialization");
                        await serverInstance.Initialize();

                        _servers[serverInstance.Id] = serverInstance;
                        Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_MONITORING_TEXT"].FormatExt(serverInstance.Hostname.StripColors()));
                        _logger.LogInformation("Finishing initialization and now monitoring [{Server}]", serverInstance.Hostname);
                    }

                    QueueEvent(new MonitorStartEvent
                    {
                        Server = serverInstance,
                        Source = this
                    });                    
                    
                    successServers++;
                }

                catch (ServerException e)
                {
                    Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_UNFIXABLE"].FormatExt($"[{configuration.IPAddress}:{configuration.Port}]"));
                    using (LogContext.PushProperty("Server", $"{configuration.IPAddress}:{configuration.Port}"))
                    {
                        _logger.LogError(e, "Unexpected exception occurred during initialization");
                    }
                    lastException = e;
                }
            }

            await Task.WhenAll(_appConfig.Servers.Select(InitializeEachServer).ToArray());

            if (successServers == 0 && lastException != null)
            {
                throw lastException;
            }

            if (successServers != _appConfig.Servers.Length && !AppContext.TryGetSwitch("NoConfirmPrompt", out _))
            {
                if (!Utilities.CurrentLocalization.LocalizationIndex["MANAGER_START_WITH_ERRORS"].PromptBool())
                {
                    throw lastException;
                }
            }
        }

        public async Task Start()
        {
            _eventHandlerTokenSource = new CancellationTokenSource();

            var eventHandlerThread = new Thread(() =>
            {
                _coreEventHandler.StartProcessing(_eventHandlerTokenSource.Token);
            })
            {
                Name = nameof(CoreEventHandler),
                IsBackground = true
            };

            eventHandlerThread.Start();
            try
            {
                await UpdateServerStates();
            }
            catch (OperationCanceledException)
            {
                // shutdown sentinel in UpdateServerStates faults Task.WhenAll on cancellation;
                // swallow so the cleanup below still runs
            }
            finally
            {
                _eventHandlerTokenSource.Cancel();
                eventHandlerThread.Join();
            }
        }

        public async Task Stop()
        {
            // Detach the config-watcher callback before cancelling the token so a
            // concurrent file change can't re-enter SyncServersWithConfigurationAsync
            // on a manager that's already shutting down. The watcher and handler are
            // owned by the DI container and will be replaced on restart.
            ConfigHandler.Updated -= SyncServersWithConfigurationAsync;
            try
            {
                _watcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing configuration watcher during shutdown");
            }

            foreach (var plugin in Plugins.Where(plugin => !plugin.IsParser))
            {
                try
                {
                    await plugin.OnUnloadAsync().WithTimeout(Utilities.DefaultCommandTimeout);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not cleanly unload plugin {PluginName}", plugin.Name);
                }
            }

            await _isRunningTokenSource.CancelAsync();

            IsRunning = false;
        }

        public async Task Restart()
        {
            IsRestartRequested = true;
            await Stop();

            using var subscriptionTimeoutToken = new CancellationTokenSource();
            subscriptionTimeoutToken.CancelAfter(Utilities.DefaultCommandTimeout);

            await IManagementEventSubscriptions.InvokeUnloadAsync(this, subscriptionTimeoutToken.Token);

            IGameEventSubscriptions.ClearEventInvocations();
            IGameServerEventSubscriptions.ClearEventInvocations();
            IManagementEventSubscriptions.ClearEventInvocations();

            // Token sources are deliberately NOT disposed/recreated here. Start() is still
            // awaiting UpdateServerStates on _isRunningTokenSource, and the event handler
            // thread is still reading _eventHandlerTokenSource.Token. Disposing either one
            // races with the unwind in Start(). LaunchAsync constructs a fresh
            // ApplicationManager via the DI container on its restart loop, so the current
            // instance (and both CTSs) become garbage after this method returns.
        }

        [Obsolete]
        public ObsoleteLogger GetLogger(long serverId)
        {
            return _serviceProvider.GetRequiredService<ObsoleteLogger>();
        }

        public IList<MessageToken> GetMessageTokens()
        {
            return MessageTokens;
        }

        public IList<EFClient> GetActiveClients()
        {
            // we're adding another to list here so we don't get a collection modified exception..
            return _servers.Values.SelectMany(s => s.Clients).ToList().Where(p => p != null).ToList();
        }

        public EFClient FindActiveClient(EFClient client) => client.ClientNumber < 0 ?
                GetActiveClients()
                    .FirstOrDefault(c => c.NetworkId == client.NetworkId && c.GameName == client.GameName) ?? client :
                client;

        public ClientService GetClientService()
        {
            return ClientSvc;
        }

        public PenaltyService GetPenaltyService()
        {
            return PenaltySvc;
        }

        public IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings()
        {
            return _legacyConfigHandler;
        }

        public void AddEvent(GameEvent gameEvent)
        {
            _coreEventHandler.QueueEvent(this, gameEvent);
        }

        public void QueueEvent(CoreEvent coreEvent)
        {
            _coreEventHandler.QueueEvent(this, coreEvent);
        }
        
        public IPageList GetPageList()
        {
            return PageList;
        }

        public IRConParser GenerateDynamicRConParser(string name)
        {
            return new DynamicRConParser(_serviceProvider.GetRequiredService<ILogger<DynamicRConParser>>(), _parserRegexFactory)
            {
                Name = name
            };
        }

        public IEventParser GenerateDynamicEventParser(string name)
        {
            return new DynamicEventParser(_parserRegexFactory, _serviceProvider.GetRequiredService<ILogger<DynamicEventParser>>(), _appConfig, _serviceProvider.GetRequiredService<IGameScriptEventFactory>())
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

        public void AddAdditionalCommand(IManagerCommand command)
        {
            lock (_commands)
            {
                if (_commands.Any(cmd => cmd.Name == command.Name || cmd.Alias == command.Alias))
                {
                    throw new InvalidOperationException(
                        $"Duplicate command name or alias ({command.Name}, {command.Alias})");
                }

                _commands.Add(command);
            }
        }

        public void RemoveCommandByName(string commandName)
        {
            lock (_commands)
            {
                _commands.RemoveAll(command => command.Name == commandName);
            }
        }

        public IAlertManager AlertManager => _alertManager;
        
        public async Task<Server> AddServerAsync(ServerConfiguration config, bool persistConfig = true, CancellationToken token = default)
        {
            var serverKey = $"{config.IPAddress}:{config.Port}";
            
            // Check if server already exists
            if (_servers.ContainsKey(serverKey))
            {
                _logger.LogWarning("Server {ServerKey} is already being monitored", serverKey);
                return null;
            }
            
            try
            {
                // Configure parsers for the server
                foreach (var parser in AdditionalRConParsers)
                {
                    config.AddRConParser(parser);
                }

                foreach (var parser in AdditionalEventParsers)
                {
                    config.AddEventParser(parser);
                }

                var serverInstance = _serverInstanceFactory.CreateServer(config, this) as IW4MServer;
                
                using (LogContext.PushProperty("Server", serverInstance!.ToString()))
                {
                    _logger.LogInformation("Beginning dynamic server initialization for {ServerKey}", serverKey);
                    await serverInstance.Initialize();
                    
                    // Create a cancellation token for this specific server
                    var serverTokenSource = new CancellationTokenSource();
                    _serverCancellationTokens[serverInstance.Id] = serverTokenSource;
                    
                    // Add to server collection (TryAdd guards against duplicate from concurrent sync)
                    if (!_servers.TryAdd(serverInstance.Id, serverInstance))
                    {
                        _logger.LogWarning("Server {ServerKey} was already added by another operation, disposing duplicate", serverKey);
                        serverTokenSource.Dispose();
                        _serverCancellationTokens.TryRemove(serverInstance.Id, out _);
                        if (serverInstance is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        return null;
                    }
                    
                    Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_MONITORING_TEXT"]
                        .FormatExt(serverInstance.Hostname.StripColors()));
                    _logger.LogInformation("Finishing initialization and now monitoring [{Server}]", serverInstance.Hostname);
                }
                
                // Queue events for the new server
                QueueEvent(new MonitorStartEvent
                {
                    Server = serverInstance,
                    Source = this
                });
                
                QueueEvent(new ServerAddEvent
                {
                    Server = serverInstance,
                    Source = this
                });
                
                // Start the update handler for this server
                _ = Task.Run(() => ProcessUpdateHandler(serverInstance, _servers.Count - 1), token);
                
                // Persist configuration if requested
                if (persistConfig)
                {
                    await PersistServerConfigurationAsync(config);
                }
                
                return serverInstance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add server {ServerKey} dynamically", serverKey);
                throw;
            }
        }
        
        public async Task<bool> RemoveServerAsync(string serverId, bool persistConfig = false, CancellationToken token = default)
        {
            // Try to find the server - serverId could be "IP:Port" format or internal Id
            Server serverToRemove = null;
            string serverKey = null;
            
            foreach (var kvp in _servers)
            {
                if (kvp.Key != serverId && kvp.Value.Id != serverId &&
                    $"{kvp.Value.ListenAddress}:{kvp.Value.ListenPort}" != serverId)
                {
                    continue;
                }

                serverKey = kvp.Key;
                serverToRemove = kvp.Value;
                break;
            }
            
            if (serverToRemove == null)
            {
                _logger.LogWarning("Server {ServerId} not found for removal", serverId);
                return false;
            }

            if (serverToRemove is DummyServer)
            {
                _logger.LogInformation("Server {ServerId} is a dummy server, skipping removal...", serverId);
                return false;
            }
            
            try
            {
                using (LogContext.PushProperty("Server", serverToRemove.ToString()))
                {
                    _logger.LogInformation("Beginning dynamic server removal for {ServerId}", serverId);
                    
                    // First, fire the MonitorStopEvent so plugins can sync data
                    QueueEvent(new MonitorStopEvent
                    {
                        Server = serverToRemove,
                        Source = this
                    });
                    
                    // Give plugins a moment to handle the event
                    await Task.Delay(500, token);
                    
                    // Cancel the per-server update handler
                    if (_serverCancellationTokens.TryRemove(serverToRemove.Id, out var serverTokenSource))
                    {
                        await serverTokenSource.CancelAsync();
                        serverTokenSource.Dispose();
                    }
                    
                    // Disconnect all clients gracefully
                    foreach (var client in serverToRemove.GetClientsAsList())
                    {
                        await client.OnDisconnect();
                    }

                    if (serverToRemove is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    
                    // Remove from the collection
                    if (!_servers.TryRemove(serverKey, out _))
                    {
                        _logger.LogWarning("Failed to remove server {ServerKey} from collection", serverKey);
                        return false;
                    }
                    
                    // Fire the ServerRemovedEvent
                    QueueEvent(new ServerRemoveEvent
                    {
                        Server = serverToRemove,
                        Source = this
                    });
                    
                    Console.WriteLine($"Server {serverToRemove.Hostname.StripColors()} has been removed");
                    _logger.LogInformation("Server {ServerId} successfully removed", serverId);
                    
                    // Persist configuration if requested
                    if (persistConfig)
                    {
                        await RemoveServerConfigurationAsync(serverToRemove);
                    }
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove server {ServerId} dynamically", serverId);
                return false;
            }
        }
        
        private async Task PersistServerConfigurationAsync(ServerConfiguration config)
        {
            var existingServers = _appConfig.Servers?.ToList() ?? [];
            existingServers.Add(config);
            _appConfig.Servers = existingServers.ToArray();
            await ConfigHandler.Set(_appConfig);
            _logger.LogInformation("Server configuration persisted to file");
        }
        
        private async Task RemoveServerConfigurationAsync(Server server)
        {
            if (_appConfig.Servers == null)
            {
                return;
            }
            
            var updatedServers = _appConfig.Servers
                .Where(s => $"{s.IPAddress}:{s.Port}" != $"{server.ListenAddress}:{server.ListenPort}")
                .ToArray();
            
            _appConfig.Servers = updatedServers;
            await ConfigHandler.Set(_appConfig);
            _logger.LogInformation("Server configuration removed from file");
        }
        
        private async void SyncServersWithConfigurationAsync(ApplicationConfiguration newConfig)
        {
            // async void — any exception that escapes this method crashes the process,
            // so every path below must swallow. The manager from a previous generation
            // can still receive this callback after Restart() has cancelled its token
            // (the old ConfigurationWatcher/ConfigHandler stay alive until GC), which
            // is why the cancellation token is treated as "skip" rather than an error.
            if (_isRunningTokenSource.IsCancellationRequested)
            {
                return;
            }

            _pendingSyncConfig = newConfig;

            var entered = false;
            try
            {
                entered = await _syncSemaphore.WaitAsync(0, CancellationToken);
                if (!entered)
                {
                    _logger.LogDebug("Config sync already in progress, queued for re-sync");
                    return;
                }

                while (Interlocked.Exchange(ref _pendingSyncConfig, null) is { } configToSync)
                {
                    await ReconcileServersWithConfigAsync(configToSync);
                }
            }
            catch (OperationCanceledException)
            {
                // manager is shutting down or being restarted — drop the sync
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing servers with configuration");
            }
            finally
            {
                if (entered)
                {
                    _syncSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// Diffs running servers against the provided configuration and applies changes.
        /// Servers present in config but not running are added; servers running but absent from config are removed.
        /// Safe to call with an unchanged config — produces a no-op when there is no diff.
        /// </summary>
        private async Task ReconcileServersWithConfigAsync(ApplicationConfiguration newConfig)
        {
            _logger.LogInformation("Syncing servers with configuration file...");

            var configuredServers = newConfig?.Servers ?? [];

            // Get current running server IDs
            var currentServerIds = _servers.Keys.ToHashSet();

            // Build configured server IDs
            var configuredServerIds = configuredServers
                .Select(s => $"{s.IPAddress}:{s.Port}")
                .ToHashSet();

            // Find servers to remove (in current but not in config)
            var serversToRemove = currentServerIds.Except(configuredServerIds).ToList();

            // Find servers to add (in config but not current)
            var serversToAdd = configuredServers
                .Where(s => !currentServerIds.Contains($"{s.IPAddress}:{s.Port}"))
                .ToList();

            // Remove servers that are no longer in config
            foreach (var serverId in serversToRemove)
            {
                _logger.LogInformation("Configuration sync: Removing server {ServerId}", serverId);
                _serverCancellationTokens.TryGetValue(serverId, out var serverTokenSource);
                await RemoveServerAsync(serverId, persistConfig: false, serverTokenSource?.Token ?? CancellationToken.None);
            }

            // Add new servers from config
            foreach (var serverConfig in serversToAdd)
            {
                _logger.LogInformation("Configuration sync: Adding server {IPAddress}:{Port}",
                    serverConfig.IPAddress, serverConfig.Port);
                await AddServerAsync(serverConfig, persistConfig: false, CancellationToken.None);
            }

            _logger.LogInformation("Configuration sync complete. Added: {Added}, Removed: {Removed}",
                serversToAdd.Count, serversToRemove.Count);
        }
        
        private async Task OnServerValueRequested(ServerValueRequestEvent requestEvent, CancellationToken token)
        {
            if (requestEvent.Server is not IW4MServer server)
            {
                return;
            }

            Dvar<string> serverValue = null;
            try
            {
                if (requestEvent.DelayMs.HasValue)
                {
                    await Task.Delay(requestEvent.DelayMs.Value, token);
                }

                var waitToken = token;
                using var timeoutTokenSource = new CancellationTokenSource();
                using var linkedTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token);
                
                if (requestEvent.TimeoutMs is not null)
                {
                    timeoutTokenSource.CancelAfter(requestEvent.TimeoutMs.Value);
                    waitToken = linkedTokenSource.Token;
                }

                serverValue =
                    await server.GetDvarAsync(requestEvent.ValueName, requestEvent.FallbackValue, waitToken);
            }
            catch
            {
                //  ignored
            }
            finally
            {
                QueueEvent(new ServerValueReceiveEvent
                {
                    Server = server,
                    Source = server,
                    Response = serverValue ?? new Dvar<string> { Name = requestEvent.ValueName },
                    Success = serverValue is not null
                });
            }
        }

        private Task OnServerValueSetRequested(ServerValueSetRequestEvent requestEvent, CancellationToken token)
        {
            return ExecuteWrapperForServerQuery(requestEvent, token, async (innerEvent) =>
            {
                if (innerEvent.DelayMs.HasValue)
                {
                    await Task.Delay(innerEvent.DelayMs.Value, token);
                }

                if (innerEvent.TimeoutMs is not null)
                {
                    using var timeoutTokenSource = new CancellationTokenSource(innerEvent.TimeoutMs.Value);
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token);
                    token = linkedTokenSource.Token;
                }

                await innerEvent.Server.SetDvarAsync(innerEvent.ValueName, innerEvent.Value, token);
            }, (completed, innerEvent) =>
            {
                QueueEvent(new ServerValueSetCompleteEvent
                {
                    Server = innerEvent.Server,
                    Source = innerEvent.Server,
                    Success = completed,
                    Value = innerEvent.Value,
                    ValueName = innerEvent.ValueName
                });
                return Task.CompletedTask;
            });
        }

        private Task OnServerCommandExecuteRequested(ServerCommandRequestExecuteEvent executeEvent, CancellationToken token)
        {
            return ExecuteWrapperForServerQuery(executeEvent, token, async (innerEvent) =>
            {
                if (innerEvent.DelayMs.HasValue)
                {
                    await Task.Delay(innerEvent.DelayMs.Value, token);
                }

                if (innerEvent.TimeoutMs is not null)
                {
                    using var timeoutTokenSource = new CancellationTokenSource(innerEvent.TimeoutMs.Value);
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token);
                    token = linkedTokenSource.Token;
                }

                await innerEvent.Server.ExecuteCommandAsync(innerEvent.Command, token);
            }, (_, __) => Task.CompletedTask);
        }

        private async Task ExecuteWrapperForServerQuery<TEventType>(TEventType serverEvent, CancellationToken token,
            Func<TEventType, Task> action, Func<bool, TEventType, Task> complete) where TEventType : GameServerEvent
        {
            if (serverEvent.Server is not IW4MServer)
            {
                return;
            }

            var completed = false;
            try
            {
                await action(serverEvent);
                completed = true;
            }
            catch
            {
                //  ignored
            }
            finally
            {
                await complete(completed, serverEvent);
            }
        }

        private async Task OnClientPersistentIdReceived(ClientPersistentIdReceiveEvent receiveEvent, CancellationToken token)
        {
            var parts = receiveEvent.PersistentId.Split(",");

            if (parts.Length == 2 && int.TryParse(parts[0], out var high) &&
                int.TryParse(parts[1], out var low))
            {
                var guid = long.Parse(high.ToString("X8") + low.ToString("X8"), NumberStyles.HexNumber);

                var penalties = await PenaltySvc
                    .GetActivePenaltiesByIdentifier(null, guid, receiveEvent.Client.GameName);
                var banPenalty =
                    penalties.FirstOrDefault(penalty => penalty.Type == EFPenalty.PenaltyType.Ban);

                if (banPenalty is not null && receiveEvent.Client.Level != Data.Models.Client.EFClient.Permission.Banned)
                {
                    _logger.LogInformation(
                        "Banning {Client} as they have have provided a persistent clientId of {PersistentClientId}, which is banned",
                        receiveEvent.Client, guid);
                    receiveEvent.Client.Ban(_translationLookup["SERVER_BAN_EVADE"].FormatExt(guid),
                        receiveEvent.Client.CurrentServer.AsConsoleClient(), true);
                }
            }
        }
    }
}
