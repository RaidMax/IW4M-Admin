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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Context;
using IW4MAdmin.Application.Migration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Formatting;
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
        public ITokenAuthentication TokenAuthenticator { get; }
        public CancellationToken CancellationToken => _tokenSource.Token;
        public string ExternalIPAddress { get; private set; }
        public bool IsRestartRequested { get; private set; }
        public IMiddlewareActionHandler MiddlewareActionHandler { get; }
        public event EventHandler<GameEvent> OnGameEventExecuted;
        private readonly List<IManagerCommand> _commands;
        private readonly ILogger _logger;
        private readonly List<MessageToken> MessageTokens;
        private readonly ClientService ClientSvc;
        readonly PenaltyService PenaltySvc;
        public IConfigurationHandler<ApplicationConfiguration> ConfigHandler;
        readonly IPageList PageList;
        private readonly TimeSpan _throttleTimeout = new TimeSpan(0, 1, 0);
        private readonly CancellationTokenSource _tokenSource;
        private readonly Dictionary<string, Task<IList>> _operationLookup = new Dictionary<string, Task<IList>>();
        private readonly ITranslationLookup _translationLookup;
        private readonly IConfigurationHandler<CommandConfiguration> _commandConfiguration;
        private readonly IGameServerInstanceFactory _serverInstanceFactory;
        private readonly IParserRegexFactory _parserRegexFactory;
        private readonly IEnumerable<IRegisterEvent> _customParserEvents;
        private readonly IEventHandler _eventHandler;
        private readonly IScriptCommandFactory _scriptCommandFactory;
        private readonly IMetaRegistration _metaRegistration;
        private readonly IScriptPluginServiceResolver _scriptPluginServiceResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly ChangeHistoryService _changeHistoryService;
        private readonly ApplicationConfiguration _appConfig;
        public ConcurrentDictionary<long, GameEvent> ProcessingEvents { get; } = new ConcurrentDictionary<long, GameEvent>();

        public ApplicationManager(ILogger<ApplicationManager> logger, IMiddlewareActionHandler actionHandler, IEnumerable<IManagerCommand> commands,
            ITranslationLookup translationLookup, IConfigurationHandler<CommandConfiguration> commandConfiguration,
            IConfigurationHandler<ApplicationConfiguration> appConfigHandler, IGameServerInstanceFactory serverInstanceFactory,
            IEnumerable<IPlugin> plugins, IParserRegexFactory parserRegexFactory, IEnumerable<IRegisterEvent> customParserEvents,
            IEventHandler eventHandler, IScriptCommandFactory scriptCommandFactory, IDatabaseContextFactory contextFactory,
            IMetaRegistration metaRegistration, IScriptPluginServiceResolver scriptPluginServiceResolver, ClientService clientService, IServiceProvider serviceProvider,
            ChangeHistoryService changeHistoryService, ApplicationConfiguration appConfig, PenaltyService penaltyService)
        {
            MiddlewareActionHandler = actionHandler;
            _servers = new ConcurrentBag<Server>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = clientService;
            PenaltySvc = penaltyService;
            ConfigHandler = appConfigHandler;
            StartTime = DateTime.UtcNow;
            PageList = new PageList();
            AdditionalEventParsers = new List<IEventParser>() { new BaseEventParser(parserRegexFactory, logger, _appConfig) };
            AdditionalRConParsers = new List<IRConParser>() { new BaseRConParser(serviceProvider.GetRequiredService<ILogger<BaseRConParser>>(), parserRegexFactory) };
            TokenAuthenticator = new TokenAuthentication();
            _logger = logger;
            _tokenSource = new CancellationTokenSource();
            _commands = commands.ToList();
            _translationLookup = translationLookup;
            _commandConfiguration = commandConfiguration;
            _serverInstanceFactory = serverInstanceFactory;
            _parserRegexFactory = parserRegexFactory;
            _customParserEvents = customParserEvents;
            _eventHandler = eventHandler;
            _scriptCommandFactory = scriptCommandFactory;
            _metaRegistration = metaRegistration;
            _scriptPluginServiceResolver = scriptPluginServiceResolver;
            _serviceProvider = serviceProvider;
            _changeHistoryService = changeHistoryService;
            _appConfig = appConfig;
            Plugins = plugins;
        }

        public IEnumerable<IPlugin> Plugins { get; }

        public async Task ExecuteEvent(GameEvent newEvent)
        {
            ProcessingEvents.TryAdd(newEvent.Id, newEvent);
            
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
                _logger.LogDebug("Received quit signal for event id {eventId}, so we are aborting early", newEvent.Id);
            }

            catch (OperationCanceledException)
            {
                _logger.LogDebug("Received quit signal for event id {eventId}, so we are aborting early", newEvent.Id);
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
            if (newEvent.Type == EventType.Command && newEvent.ImpersonationOrigin == null)
            {
                var correlatedEvents =
                    ProcessingEvents.Values.Where(ev =>
                            ev.CorrelationId == newEvent.CorrelationId && ev.Id != newEvent.Id)
                        .ToList();

                await Task.WhenAll(correlatedEvents.Select(ev =>
                    ev.WaitAsync(Utilities.DefaultCommandTimeout, CancellationToken)));
                newEvent.Output.AddRange(correlatedEvents.SelectMany(ev => ev.Output));

                foreach (var correlatedEvent in correlatedEvents)
                {
                    ProcessingEvents.Remove(correlatedEvent.Id, out _);
                }
            }

            // we don't want to remove events that are correlated to command
            if (ProcessingEvents.Values.ToList()?.Count(gameEvent => gameEvent.CorrelationId == newEvent.CorrelationId) == 1)
            {
                ProcessingEvents.Remove(newEvent.Id, out _);
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

        public async Task UpdateServerStates()
        {
            // store the server hash code and task for it
            var runningUpdateTasks = new Dictionary<long, (Task task, CancellationTokenSource tokenSource, DateTime startTime)>();

            while (!_tokenSource.IsCancellationRequested)
            {
                // select the server ids that have completed the update task
                var serverTasksToRemove = runningUpdateTasks
                    .Where(ut => ut.Value.task.Status == TaskStatus.RanToCompletion ||
                                 ut.Value.task.Status == TaskStatus.Canceled || // we want to cancel if a task takes longer than 5 minutes
                                 ut.Value.task.Status == TaskStatus.Faulted || DateTime.Now - ut.Value.startTime > TimeSpan.FromMinutes(5))
                    .Select(ut => ut.Key)
                    .ToList();

                // remove the update tasks as they have completed
                foreach (var serverId in serverTasksToRemove.Where(serverId => runningUpdateTasks.ContainsKey(serverId)))
                {
                    if (!runningUpdateTasks[serverId].tokenSource.Token.IsCancellationRequested)
                    {
                        runningUpdateTasks[serverId].tokenSource.Cancel();
                    }

                    runningUpdateTasks.Remove(serverId);
                }

                // select the servers where the tasks have completed
                var serverIds = Servers.Select(s => s.EndPoint).Except(runningUpdateTasks.Select(r => r.Key)).ToList();
                foreach (var server in Servers.Where(s => serverIds.Contains(s.EndPoint)))
                {
                    var tokenSource = new CancellationTokenSource();
                    runningUpdateTasks.Add(server.EndPoint, (Task.Run(async () =>
                    {
                        try
                        {
                            if (runningUpdateTasks.ContainsKey(server.EndPoint))
                            {
                                await server.ProcessUpdatesAsync(_tokenSource.Token)
                                    .WithWaitCancellation(runningUpdateTasks[server.EndPoint].tokenSource.Token);
                            }
                        }

                        catch (Exception e)
                        {
                            using (LogContext.PushProperty("Server", server.ToString()))
                            {
                                _logger.LogError(e, "Failed to update status");
                            }
                        }

                        finally
                        {
                            server.IsInitialized = true;
                        }
                    }, tokenSource.Token), tokenSource, DateTime.Now));
                }

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
            IsRunning = true;
            ExternalIPAddress = await Utilities.GetExternalIP();

            #region DATABASE
            _logger.LogInformation("Beginning database migration sync");
            Console.WriteLine(_translationLookup["MANAGER_MIGRATION_START"]);
            await ContextSeed.Seed(_serviceProvider.GetRequiredService<IDatabaseContextFactory>(), _tokenSource.Token);
            await DatabaseHousekeeping.RemoveOldRatings(_serviceProvider.GetRequiredService<IDatabaseContextFactory>(), _tokenSource.Token);
            _logger.LogInformation("Finished database migration sync");
            Console.WriteLine(_translationLookup["MANAGER_MIGRATION_END"]);
            #endregion

            #region PLUGINS
            foreach (var plugin in Plugins)
            {
                try
                {
                    if (plugin is ScriptPlugin scriptPlugin)
                    {
                        await scriptPlugin.Initialize(this, _scriptCommandFactory, _scriptPluginServiceResolver);
                        scriptPlugin.Watcher.Changed += async (sender, e) =>
                        {
                            try
                            {
                                await scriptPlugin.Initialize(this, _scriptCommandFactory, _scriptPluginServiceResolver);
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
                    await ConfigHandler.Save();
                }

                if (string.IsNullOrEmpty(_appConfig.WebfrontBindUrl))
                {
                    _appConfig.WebfrontBindUrl = "http://0.0.0.0:1624";
                    await ConfigHandler.Save();
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
                    await ConfigHandler.Save();
                }
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
            if (await ClientSvc.HasOwnerAsync(_tokenSource.Token))
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
                    var ServerInstance = _serverInstanceFactory.CreateServer(Conf, this) as IW4MServer;
                    using (LogContext.PushProperty("Server", ServerInstance.ToString()))
                    {
                        _logger.LogInformation("Beginning server communication initialization");
                        await ServerInstance.Initialize();

                        _servers.Add(ServerInstance);
                        Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_MONITORING_TEXT"].FormatExt(ServerInstance.Hostname.StripColors()));
                        _logger.LogInformation("Finishing initialization and now monitoring [{Server}]", ServerInstance.Hostname);
                    }

                    // add the start event for this server
                    var e = new GameEvent()
                    {
                        Type = EventType.Start,
                        Data = $"{ServerInstance.GameName} started",
                        Owner = ServerInstance
                    };

                    AddEvent(e);
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

        public async Task Start() => await UpdateServerStates();

        public void Stop()
        {
            _tokenSource.Cancel();
            IsRunning = false;
        }

        public void Restart()
        {
            IsRestartRequested = true;
            Stop();
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

        public EFClient FindActiveClient(EFClient client) =>client.ClientNumber < 0 ?
                GetActiveClients()
                    .FirstOrDefault(c => c.NetworkId == client.NetworkId) ?? client :
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
            _eventHandler.HandleEvent(this, gameEvent);
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
            if (_commands.Any(_command => _command.Name == command.Name || _command.Alias == command.Alias))
            {
                throw new InvalidOperationException($"Duplicate command name or alias ({command.Name}, {command.Alias})");
            }

            _commands.Add(command);
        }

        public void RemoveCommandByName(string commandName) => _commands.RemoveAll(_command => _command.Name == commandName);
    }
}
