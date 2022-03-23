using IW4MAdmin.Application.API.Master;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Application.Meta;
using IW4MAdmin.Application.Migration;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.DependencyInjection;
using RestEase;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using SharedLibraryCore.Repositories;
using SharedLibraryCore.Services;
using Stats.Dtos;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Helpers;
using Integrations.Source.Extensions;
using IW4MAdmin.Application.Extensions;
using IW4MAdmin.Application.Localization;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using IW4MAdmin.Plugins.Stats.Client.Abstractions;
using IW4MAdmin.Plugins.Stats.Client;
using Stats.Client.Abstractions;
using Stats.Client;
using Stats.Config;
using Stats.Helpers;

namespace IW4MAdmin.Application
{
    public class Program
    {
        public static BuildNumber Version { get; } = BuildNumber.Parse(Utilities.GetVersionAsString());
        private static ApplicationManager _serverManager;
        private static Task _applicationTask;
        private static ServiceProvider _serviceProvider;

        /// <summary>
        /// entrypoint of the application
        /// </summary>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", Utilities.OperatingDirectory);

            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.CancelKeyPress += OnCancelKey;

            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4MAdmin");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine($" Version {Utilities.GetVersionAsString()}");
            Console.WriteLine("=====================================================");

            await LaunchAsync(args);
        }

        /// <summary>
        /// event callback executed when the control + c combination is detected
        /// gracefully stops the server manager and waits for all tasks to finish
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void OnCancelKey(object sender, ConsoleCancelEventArgs e)
        {
            _serverManager?.Stop();
            if (_applicationTask != null)
            {
                await _applicationTask;
            }
        }

        /// <summary>
        /// task that initializes application and starts the application monitoring and runtime tasks
        /// </summary>
        /// <returns></returns>
        private static async Task LaunchAsync(string[] args)
        {
            restart:
            ITranslationLookup translationLookup = null;
            var logger = BuildDefaultLogger<Program>(new ApplicationConfiguration());
            Utilities.DefaultLogger = logger;
            logger.LogInformation("Begin IW4MAdmin startup. Version is {Version} {@Args}", Version, args);
            
            try
            {
                // do any needed housekeeping file/folder migrations
                ConfigurationMigration.MoveConfigFolder10518(null);
                ConfigurationMigration.CheckDirectories();
                ConfigurationMigration.RemoveObsoletePlugins20210322();
                logger.LogDebug("Configuring services...");
                var services = await ConfigureServices(args);
                _serviceProvider = services.BuildServiceProvider();
                var versionChecker = _serviceProvider.GetRequiredService<IMasterCommunication>();
                _serverManager = (ApplicationManager) _serviceProvider.GetRequiredService<IManager>();
                translationLookup = _serviceProvider.GetRequiredService<ITranslationLookup>();

                _applicationTask = RunApplicationTasksAsync(logger, services);
                var tasks = new[]
                {
                    versionChecker.CheckVersion(),
                    _applicationTask
                };

                await _serverManager.Init();

                await Task.WhenAll(tasks);
            }

            catch (Exception e)
            {
                var failMessage = translationLookup == null
                    ? "Failed to initialize IW4MAdmin"
                    : translationLookup["MANAGER_INIT_FAIL"];
                var exitMessage = translationLookup == null
                    ? "Press enter to exit..."
                    : translationLookup["MANAGER_EXIT"];

                logger.LogCritical(e, "Failed to initialize IW4MAdmin");
                Console.WriteLine(failMessage);

                while (e.InnerException != null)
                {
                    e = e.InnerException;
                }

                if (e is ConfigurationException configException)
                {
                    Console.WriteLine("{{fileName}} contains an error."
                        .FormatExt(Path.GetFileName(configException.ConfigurationFileName)));

                    foreach (var error in configException.Errors)
                    {
                        Console.WriteLine(error);
                    }
                }

                else
                {
                    Console.WriteLine(e.Message);
                }
                
                _serverManager?.Stop();

                Console.WriteLine(exitMessage);
                await Console.In.ReadAsync(new char[1], 0, 1);
                return;
            }

            if (_serverManager.IsRestartRequested)
            {
                goto restart;
            }

            await _serviceProvider.DisposeAsync();
        }

        /// <summary>
        /// runs the core application tasks
        /// </summary>
        /// <returns></returns>
        private static async Task RunApplicationTasksAsync(ILogger logger, IServiceCollection services)
        {
            var webfrontTask = _serverManager.GetApplicationSettings().Configuration().EnableWebFront
                ? WebfrontCore.Program.Init(_serverManager, _serviceProvider, services, _serverManager.CancellationToken)
                : Task.CompletedTask;

            var collectionService = _serviceProvider.GetRequiredService<IServerDataCollector>();

            // we want to run this one on a manual thread instead of letting the thread pool handle it,
            // because we can't exit early from waiting on console input, and it prevents us from restarting
            async void ReadInput() => await ReadConsoleInput(logger);

            var inputThread = new Thread(ReadInput);
            inputThread.Start();

            var tasks = new[]
            {
                webfrontTask,
                _serverManager.Start(),
                _serviceProvider.GetRequiredService<IMasterCommunication>()
                    .RunUploadStatus(_serverManager.CancellationToken),
                collectionService.BeginCollectionAsync(cancellationToken: _serverManager.CancellationToken)
            };

            logger.LogDebug("Starting webfront and input tasks");
            await Task.WhenAll(tasks);

            logger.LogInformation("Shutdown completed successfully");
            Console.WriteLine(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_SHUTDOWN_SUCCESS"]);
        }
        
        /// <summary>
        /// reads input from the console and executes entered commands on the default server
        /// </summary>
        /// <returns></returns>
        private static async Task ReadConsoleInput(ILogger logger)
        {
            if (Console.IsInputRedirected)
            {
                logger.LogInformation("Disabling console input as it has been redirected");
                return;
            }

            EFClient origin = null;

            try
            {
                while (!_serverManager.CancellationToken.IsCancellationRequested)
                {
                    if (!_serverManager.IsInitialized)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                    
                    var lastCommand = await Console.In.ReadLineAsync();

                    if (lastCommand == null)
                    {
                        continue;
                    }

                    if (!lastCommand.Any())
                    {
                        continue;
                    }

                    var gameEvent = new GameEvent
                    {
                        Type = GameEvent.EventType.Command,
                        Data = lastCommand,
                        Origin = origin ??= Utilities.IW4MAdminClient(_serverManager.Servers.FirstOrDefault()),
                        Owner = _serverManager.Servers[0]
                    };

                    _serverManager.AddEvent(gameEvent);
                    await gameEvent.WaitAsync(Utilities.DefaultCommandTimeout, _serverManager.CancellationToken);
                    Console.Write('>');
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static IServiceCollection HandlePluginRegistration(ApplicationConfiguration appConfig,
            IServiceCollection serviceCollection,
            IMasterApi masterApi)
        {
            var defaultLogger = BuildDefaultLogger<Program>(appConfig);
            var pluginServiceProvider = new ServiceCollection()
                .AddBaseLogger(appConfig)
                .AddSingleton(appConfig)
                .AddSingleton(masterApi)
                .AddSingleton<IRemoteAssemblyHandler, RemoteAssemblyHandler>()
                .AddSingleton<IPluginImporter, PluginImporter>()
                .BuildServiceProvider();

            var pluginImporter = pluginServiceProvider.GetRequiredService<IPluginImporter>();

            // we need to register the rest client with regular collection
            serviceCollection.AddSingleton(masterApi);

            // register the native commands
            foreach (var commandType in typeof(SharedLibraryCore.Commands.QuitCommand).Assembly.GetTypes()
                .Concat(typeof(Program).Assembly.GetTypes().Where(type => type.Namespace?.StartsWith("IW4MAdmin.Application.Commands") ?? false))
                .Where(command => command.BaseType == typeof(Command)))
            {
                defaultLogger.LogDebug("Registered native command type {Name}", commandType.Name);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            // register the plugin implementations
            var (plugins, commands, configurations) = pluginImporter.DiscoverAssemblyPluginImplementations();
            foreach (var pluginType in plugins)
            {
                defaultLogger.LogDebug("Registered plugin type {Name}", pluginType.FullName);
                serviceCollection.AddSingleton(typeof(IPlugin), pluginType);
            }

            // register the plugin commands
            foreach (var commandType in commands)
            {
                defaultLogger.LogDebug("Registered plugin command type {Name}", commandType.FullName);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            foreach (var configurationType in configurations)
            {
                defaultLogger.LogDebug("Registered plugin config type {Name}", configurationType.Name);
                var configInstance = (IBaseConfiguration) Activator.CreateInstance(configurationType);
                var handlerType = typeof(BaseConfigurationHandler<>).MakeGenericType(configurationType);
                var handlerInstance = Activator.CreateInstance(handlerType, configInstance.Name());
                var genericInterfaceType = typeof(IConfigurationHandler<>).MakeGenericType(configurationType);
                
                serviceCollection.AddSingleton(genericInterfaceType, handlerInstance);
            }

            // register any script plugins
            foreach (var plugin in pluginImporter.DiscoverScriptPlugins())
            {
                serviceCollection.AddSingleton(plugin);
            }

            // register any eventable types
            foreach (var assemblyType in typeof(Program).Assembly.GetTypes()
                .Where(asmType => typeof(IRegisterEvent).IsAssignableFrom(asmType))
                .Union(plugins.SelectMany(asm => asm.Assembly.GetTypes())
                    .Distinct()
                    .Where(asmType => typeof(IRegisterEvent).IsAssignableFrom(asmType))))
            {
                var instance = Activator.CreateInstance(assemblyType) as IRegisterEvent;
                serviceCollection.AddSingleton(instance);
            }

            return serviceCollection;
        }


        /// <summary>
        /// Configures the dependency injection services
        /// </summary>
        private static async Task<IServiceCollection> ConfigureServices(string[] args)
        {
            // todo: this is a quick fix
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // setup the static resources (config/master api/translations)
            var serviceCollection = new ServiceCollection();
            var appConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
            await appConfigHandler.BuildAsync();
            var defaultConfigHandler = new BaseConfigurationHandler<DefaultSettings>("DefaultSettings");
            await defaultConfigHandler.BuildAsync();
            var commandConfigHandler = new BaseConfigurationHandler<CommandConfiguration>("CommandConfiguration");
            await commandConfigHandler.BuildAsync();
            var statsCommandHandler = new BaseConfigurationHandler<StatsConfiguration>("StatsPluginSettings");
            await statsCommandHandler.BuildAsync();
            var defaultConfig = defaultConfigHandler.Configuration();
            var appConfig = appConfigHandler.Configuration();
            var masterUri = Utilities.IsDevelopment
                ? new Uri("http://127.0.0.1:8080")
                : appConfig?.MasterUrl ?? new ApplicationConfiguration().MasterUrl;
            var httpClient = new HttpClient
            {
                BaseAddress = masterUri,
                Timeout = TimeSpan.FromSeconds(15)
            };
            var masterRestClient = RestClient.For<IMasterApi>(httpClient);
            var translationLookup = Configure.Initialize(Utilities.DefaultLogger, masterRestClient, appConfig);

            if (appConfig == null)
            {
                appConfig = (ApplicationConfiguration) new ApplicationConfiguration().Generate();
                appConfigHandler.Set(appConfig);
                await appConfigHandler.Save();
            }

            // register override level names
            foreach (var (key, value) in appConfig.OverridePermissionLevelNames)
            {
                if (!Utilities.PermissionLevelOverrides.ContainsKey(key))
                {
                    Utilities.PermissionLevelOverrides.Add(key, value);
                }
            }

            // build the dependency list
            HandlePluginRegistration(appConfig, serviceCollection, masterRestClient);

            serviceCollection
                .AddBaseLogger(appConfig)
                .AddSingleton(defaultConfig)
                .AddSingleton<IServiceCollection>(serviceCollection)
                .AddSingleton<IConfigurationHandler<DefaultSettings>, BaseConfigurationHandler<DefaultSettings>>()
                .AddSingleton((IConfigurationHandler<ApplicationConfiguration>) appConfigHandler)
                .AddSingleton<IConfigurationHandler<CommandConfiguration>>(commandConfigHandler)
                .AddSingleton(appConfig)
                .AddSingleton(statsCommandHandler.Configuration() ?? new StatsConfiguration())
                .AddSingleton(serviceProvider =>
                    serviceProvider.GetRequiredService<IConfigurationHandler<CommandConfiguration>>()
                        .Configuration() ?? new CommandConfiguration())
                .AddSingleton<IPluginImporter, PluginImporter>()
                .AddSingleton<IMiddlewareActionHandler, MiddlewareActionHandler>()
                .AddSingleton<IRConConnectionFactory, RConConnectionFactory>()
                .AddSingleton<IGameServerInstanceFactory, GameServerInstanceFactory>()
                .AddSingleton<IConfigurationHandlerFactory, ConfigurationHandlerFactory>()
                .AddSingleton<IParserRegexFactory, ParserRegexFactory>()
                .AddSingleton<IDatabaseContextFactory, DatabaseContextFactory>()
                .AddSingleton<IGameLogReaderFactory, GameLogReaderFactory>()
                .AddSingleton<IScriptCommandFactory, ScriptCommandFactory>()
                .AddSingleton<IAuditInformationRepository, AuditInformationRepository>()
                .AddSingleton<IEntityService<EFClient>, ClientService>()
                .AddSingleton<IMetaService, MetaService>()
                .AddSingleton<IMetaServiceV2, MetaServiceV2>()
                .AddSingleton<ClientService>()
                .AddSingleton<PenaltyService>()
                .AddSingleton<ChangeHistoryService>()
                .AddSingleton<IMetaRegistration, MetaRegistration>()
                .AddSingleton<IScriptPluginServiceResolver, ScriptPluginServiceResolver>()
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse>,
                    ReceivedPenaltyResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse>,
                    AdministeredPenaltyResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse>,
                    UpdatedAliasResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ChatSearchQuery, MessageResponse>, ChatResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, ConnectionHistoryResponse>, ConnectionsResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, PermissionLevelChangedResponse>, PermissionLevelChangedResourceQueryHelper>()
                .AddTransient<IParserPatternMatcher, ParserPatternMatcher>()
                .AddSingleton<IRemoteAssemblyHandler, RemoteAssemblyHandler>()
                .AddSingleton<IMasterCommunication, MasterCommunication>()
                .AddSingleton<IManager, ApplicationManager>()
#pragma warning disable CS0612
                .AddSingleton<SharedLibraryCore.Interfaces.ILogger, Logger>()
#pragma warning restore CS0612
                .AddSingleton<IClientNoticeMessageFormatter, ClientNoticeMessageFormatter>()
                .AddSingleton<IClientStatisticCalculator, HitCalculator>()
                .AddSingleton<IServerDistributionCalculator, ServerDistributionCalculator>()
                .AddSingleton<IWeaponNameParser, WeaponNameParser>()
                .AddSingleton<IHitInfoBuilder, HitInfoBuilder>()
                .AddSingleton(typeof(ILookupCache<>), typeof(LookupCache<>))
                .AddSingleton(typeof(IDataValueCache<,>), typeof(DataValueCache<,>))
                .AddSingleton<IServerDataViewer, ServerDataViewer>()
                .AddSingleton<IServerDataCollector, ServerDataCollector>()
                .AddSingleton<IEventPublisher, EventPublisher>()
                .AddTransient<IScriptPluginTimerHelper, ScriptPluginTimerHelper>()
                .AddSingleton(translationLookup)
                .AddDatabaseContextOptions(appConfig);

            if (args.Contains("serialevents"))
            {
                serviceCollection.AddSingleton<IEventHandler, SerialGameEventHandler>();
            }
            else
            {
                serviceCollection.AddSingleton<IEventHandler, GameEventHandler>();
            }

            serviceCollection.AddSource();

            return serviceCollection;
        }

        private static ILogger BuildDefaultLogger<T>(ApplicationConfiguration appConfig)
        {
            var collection = new ServiceCollection()
                .AddBaseLogger(appConfig)
                .BuildServiceProvider();

            return collection.GetRequiredService<ILogger<T>>();
        }
    }
}
