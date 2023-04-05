using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Extensions;
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
        private readonly ConcurrentBag<Server> _servers;
        public List<Server> Servers => _servers.OrderByDescending(s => s.ClientNum).ToList();
        [Obsolete] public ObsoleteLogger Logger => _serviceProvider.GetRequiredService<ObsoleteLogger>();
        public bool IsRunning { get; private set; }
        public bool IsInitialized { get; private set; }
        public DateTime StartTime { get; private set; }
        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        public IList<IRConParser> AdditionalRConParsers { get; }
        public IList<IEventParser> AdditionalEventParsers { get; }
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
        public IConfigurationHandler<ApplicationConfiguration> ConfigHandler;
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
        public ConcurrentDictionary<long, GameEvent> ProcessingEvents { get; } = new();

        public ApplicationManager(ILogger<ApplicationManager> logger, IMiddlewareActionHandler actionHandler, IEnumerable<IManagerCommand> commands,
            ITranslationLookup translationLookup, IConfigurationHandler<CommandConfiguration> commandConfiguration,
            IConfigurationHandler<ApplicationConfiguration> appConfigHandler, IGameServerInstanceFactory serverInstanceFactory,
            IEnumerable<IPlugin> plugins, IParserRegexFactory parserRegexFactory, IEnumerable<IRegisterEvent> customParserEvents,
            ICoreEventHandler coreEventHandler, IScriptCommandFactory scriptCommandFactory, IDatabaseContextFactory contextFactory,
            IMetaRegistration metaRegistration, IScriptPluginServiceResolver scriptPluginServiceResolver, ClientService clientService, IServiceProvider serviceProvider,
            ChangeHistoryService changeHistoryService, ApplicationConfiguration appConfig, PenaltyService penaltyService, IAlertManager alertManager, IInteractionRegistration interactionRegistration, IEnumerable<IPluginV2> v2PLugins)
        {
            MiddlewareActionHandler = actionHandler;
            _servers = new ConcurrentBag<Server>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = clientService;
            PenaltySvc = penaltyService;
            _alertManager = alertManager;
            ConfigHandler = appConfigHandler;
            StartTime = DateTime.UtcNow;
            PageList = new PageList();
            AdditionalEventParsers = new List<IEventParser> { new BaseEventParser(parserRegexFactory, logger, _appConfig) };
            AdditionalRConParsers = new List<IRConParser> { new BaseRConParser(serviceProvider.GetRequiredService<ILogger<BaseRConParser>>(), parserRegexFactory) };
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
            return Servers;
        }

        public IList<IManagerCommand> GetCommands()
        {
            return _commands;
        }

        public IReadOnlyList<IManagerCommand> Commands => _commands.ToImmutableList();

        private Task UpdateServerStates()
        {
            var index = 0;
            return Task.WhenAll(_servers.Select(server =>
            {
                var thisIndex = index;
                Interlocked.Increment(ref index);
                return ProcessUpdateHandler(server, thisIndex);
            }));
        }

        private async Task ProcessUpdateHandler(Server server, int index)
        {
            const int delayScalar = 50; // Task.Delay is inconsistent enough there's no reason to try to prevent collisions
            var timeout = TimeSpan.FromMinutes(2);

            while (!_isRunningTokenSource.IsCancellationRequested)
            {
                try
                {
                    var delayFactor = Math.Min(_appConfig.RConPollRate, delayScalar * index);
                    await Task.Delay(delayFactor, _isRunningTokenSource.Token);

                    using var timeoutTokenSource = new CancellationTokenSource();
                    timeoutTokenSource.CancelAfter(timeout);
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token,
                            _isRunningTokenSource.Token);
                    await server.ProcessUpdatesAsync(linkedTokenSource.Token);

                    await Task.Delay(Math.Max(1000, _appConfig.RConPollRate - delayFactor),
                        _isRunningTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // ignored
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
            await server.ProcessUpdatesAsync(_isRunningTokenSource.Token);
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

                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_translationLookup["SERVER_ERROR_PLUGIN"]} {plugin.Name}");
                }
            }
            #endregion

            #region CONFIG
            // copy over default config if it doesn't exist
            if (!_appConfig.Servers?.Any() ?? true)
            {
                var defaultHandler = new BaseConfigurationHandler<DefaultSettings>("DefaultSettings");
                await defaultHandler.BuildAsync();
                var defaultConfig = defaultHandler.Configuration();
        
                _appConfig.AutoMessages = defaultConfig.AutoMessages;
                _appConfig.GlobalRules = defaultConfig.GlobalRules;
                _appConfig.DisallowedClientNames = defaultConfig.DisallowedClientNames;

                //if (newConfig.Servers == null)
                {
                    ConfigHandler.Set(_appConfig);
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

                    await ConfigHandler.Save();
                }
            }

            else
            {
                if (string.IsNullOrEmpty(_appConfig.Id))
                {
                    _appConfig.Id = Guid.NewGuid().ToString();
                }

                if (string.IsNullOrEmpty(_appConfig.WebfrontBindUrl))
                {
                    _appConfig.WebfrontBindUrl = "http://0.0.0.0:1624";
                }

#pragma warning disable 618
                if (_appConfig.Maps != null)
                {
                    _appConfig.Maps = null;
                }

                if (_appConfig.QuickMessages != null)
                {
                    _appConfig.QuickMessages = null;
                }
#pragma warning restore 618

                var validator = new ApplicationConfigurationValidator();
                var validationResult = validator.Validate(_appConfig);

                if (!validationResult.IsValid)
                {
                    throw new ConfigurationException("Could not validate configuration")
                    {
                        Errors = validationResult.Errors.Select(_error => _error.ErrorMessage).ToArray(),
                        ConfigurationFileName = ConfigHandler.FileName
                    };
                }

                foreach (var serverConfig in _appConfig.Servers)
                {
                    ConfigurationMigration.ModifyLogPath020919(serverConfig);

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
                await ConfigHandler.Save();
            }

            if (_appConfig.Servers.Length == 0)
            {
                throw new ServerException("A server configuration in IW4MAdminSettings.json is invalid");
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
                _commands.RemoveAll(_cmd => _cmd.GetType() == typeof(OwnerCommand));
            }

            List<IManagerCommand> commandsToAddToConfig = new List<IManagerCommand>();
            var cmdConfig = _commandConfiguration.Configuration();

            if (cmdConfig == null)
            {
                cmdConfig = new CommandConfiguration();
                commandsToAddToConfig.AddRange(_commands);
            }

            else
            {
                var unsavedCommands = _commands.Where(_cmd => !cmdConfig.Commands.Keys.Contains(_cmd.CommandConfigNameForType()));
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
            IsInitialized = true;
        }

        private async Task InitializeServers()
        {
            var config = ConfigHandler.Configuration();
            int successServers = 0;
            Exception lastException = null;

            async Task Init(ServerConfiguration Conf)
            {
                try
                {
                    // todo: this might not always be an IW4MServer
                    var serverInstance = _serverInstanceFactory.CreateServer(Conf, this) as IW4MServer;
                    using (LogContext.PushProperty("Server", serverInstance!.ToString()))
                    {
                        _logger.LogInformation("Beginning server communication initialization");
                        await serverInstance.Initialize();

                        _servers.Add(serverInstance);
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
                    Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_UNFIXABLE"].FormatExt($"[{Conf.IPAddress}:{Conf.Port}]"));
                    using (LogContext.PushProperty("Server", $"{Conf.IPAddress}:{Conf.Port}"))
                    {
                        _logger.LogError(e, "Unexpected exception occurred during initialization");
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

        public async Task Start()
        {
            _eventHandlerTokenSource = new CancellationTokenSource();
            
            var eventHandlerThread = new Thread(() =>
            {
                _coreEventHandler.StartProcessing(_eventHandlerTokenSource.Token);
            })
            {
                Name = nameof(CoreEventHandler)
            };

            eventHandlerThread.Start();
            await UpdateServerStates();
            _eventHandlerTokenSource.Cancel();
            eventHandlerThread.Join();
        }

        public async Task Stop()
        {
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

            _isRunningTokenSource.Cancel();

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
            
            _isRunningTokenSource.Dispose();
            _isRunningTokenSource = new CancellationTokenSource();
            
            _eventHandlerTokenSource.Dispose();
            _eventHandlerTokenSource = new CancellationTokenSource();
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
            return _servers.SelectMany(s => s.Clients).ToList().Where(p => p != null).ToList();
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
            return ConfigHandler;
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
            return new DynamicRConParser(_serviceProvider.GetRequiredService<ILogger<BaseRConParser>>(), _parserRegexFactory)
            {
                Name = name
            };
        }

        public IEventParser GenerateDynamicEventParser(string name)
        {
            return new DynamicEventParser(_parserRegexFactory, _logger, ConfigHandler.Configuration())
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

        public void RemoveCommandByName(string commandName) => _commands.RemoveAll(_command => _command.Name == commandName);
        public IAlertManager AlertManager => _alertManager;
        
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

        private async Task OnServerValueSetRequested(ServerValueSetRequestEvent requestEvent, CancellationToken token)
        {
            if (requestEvent.Server is not IW4MServer server)
            {
                return;
            }

            var completed = false;
            try
            {
                if (requestEvent.DelayMs.HasValue)
                {
                    await Task.Delay(requestEvent.DelayMs.Value, token);
                }

                if (requestEvent.TimeoutMs is not null)
                {
                    using var timeoutTokenSource = new CancellationTokenSource(requestEvent.TimeoutMs.Value);
                    using var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token);
                    token = linkedTokenSource.Token;
                }

                await server.SetDvarAsync(requestEvent.ValueName, requestEvent.Value, token);
                completed = true;
            }
            catch
            {
                //  ignored
            }
            finally
            {
                QueueEvent(new ServerValueSetCompleteEvent
                {
                    Server = server,
                    Source = server,
                    Success = completed,
                    Value = requestEvent.Value,
                    ValueName = requestEvent.ValueName
                });
            }
        }

        private async Task OnClientPersistentIdReceived(ClientPersistentIdReceiveEvent receiveEvent, CancellationToken token)
        {
            var parts = receiveEvent.PersistentId.Split(",");

            if (parts.Length == 2 && int.TryParse(parts[0], out var high) &&
                int.TryParse(parts[1], out var low))
            {
                var guid = long.Parse(high.ToString("X") + low.ToString("X"), NumberStyles.HexNumber);

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
