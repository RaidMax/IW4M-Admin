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
using Stats.Helpers;

namespace IW4MAdmin.Application
{
    public class Program
    {
        public static BuildNumber Version { get; } = BuildNumber.Parse(Utilities.GetVersionAsString());
        public static ApplicationManager ServerManager;
        private static Task ApplicationTask;
        private static ServiceProvider serviceProvider;

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
            ServerManager?.Stop();
            if (ApplicationTask != null)
            {
                await ApplicationTask;
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
            IServiceCollection services = null;
            logger.LogInformation("Begin IW4MAdmin startup. Version is {Version} {@Args}", Version, args);
            
            try
            {
                // do any needed housekeeping file/folder migrations
                ConfigurationMigration.MoveConfigFolder10518(null);
                ConfigurationMigration.CheckDirectories();
                ConfigurationMigration.RemoveObsoletePlugins20210322();
                logger.LogDebug("Configuring services...");
                services = ConfigureServices(args);
                serviceProvider = services.BuildServiceProvider();
                var versionChecker = serviceProvider.GetRequiredService<IMasterCommunication>();
                ServerManager = (ApplicationManager) serviceProvider.GetRequiredService<IManager>();
                translationLookup = serviceProvider.GetRequiredService<ITranslationLookup>();

                ApplicationTask = RunApplicationTasksAsync(logger, services);
                var tasks = new[]
                {
                    versionChecker.CheckVersion(),
                    ServerManager.Init(),
                    ApplicationTask
                };

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
                    if (translationLookup != null)
                    {
                        Console.WriteLine(translationLookup[configException.Message]
                            .FormatExt(configException.ConfigurationFileName));
                    }

                    foreach (var error in configException.Errors)
                    {
                        Console.WriteLine(error);
                    }
                }

                else
                {
                    Console.WriteLine(e.Message);
                }

                Console.WriteLine(exitMessage);
                await Console.In.ReadAsync(new char[1], 0, 1);
                return;
            }

            if (ServerManager.IsRestartRequested)
            {
                goto restart;
            }

            await serviceProvider.DisposeAsync();
        }

        /// <summary>
        /// runs the core application tasks
        /// </summary>
        /// <returns></returns>
        private static async Task RunApplicationTasksAsync(ILogger logger, IServiceCollection services)
        {
            var webfrontTask = ServerManager.GetApplicationSettings().Configuration().EnableWebFront
                ? WebfrontCore.Program.Init(ServerManager, serviceProvider, services, ServerManager.CancellationToken)
                : Task.CompletedTask;

            var collectionService = serviceProvider.GetRequiredService<IServerDataCollector>();

            // we want to run this one on a manual thread instead of letting the thread pool handle it,
            // because we can't exit early from waiting on console input, and it prevents us from restarting
            async void ReadInput() => await ReadConsoleInput(logger);

            var inputThread = new Thread(ReadInput);
            inputThread.Start();

            var tasks = new[]
            {
                webfrontTask,
                ServerManager.Start(),
                serviceProvider.GetRequiredService<IMasterCommunication>()
                    .RunUploadStatus(ServerManager.CancellationToken),
                collectionService.BeginCollectionAsync(cancellationToken: ServerManager.CancellationToken)
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

            string lastCommand;
            EFClient origin = null;

            try
            {
                while (!ServerManager.CancellationToken.IsCancellationRequested)
                {
                    lastCommand = await Console.In.ReadLineAsync();

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
                        Origin = origin ??= Utilities.IW4MAdminClient(ServerManager.Servers.FirstOrDefault()),
                        Owner = ServerManager.Servers[0]
                    };

                    ServerManager.AddEvent(gameEvent);
                    await gameEvent.WaitAsync(Utilities.DefaultCommandTimeout, ServerManager.CancellationToken);
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
                .Concat(typeof(Program).Assembly.GetTypes().Where(type => type.Namespace == "IW4MAdmin.Application.Commands"))
                .Where(_command => _command.BaseType == typeof(Command)))
            {
                defaultLogger.LogDebug("Registered native command type {name}", commandType.Name);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            // register the plugin implementations
            var (plugins, commands, configurations) = pluginImporter.DiscoverAssemblyPluginImplementations();
            foreach (var pluginType in plugins)
            {
                defaultLogger.LogDebug("Registered plugin type {name}", pluginType.FullName);
                serviceCollection.AddSingleton(typeof(IPlugin), pluginType);
            }

            // register the plugin commands
            foreach (var commandType in commands)
            {
                defaultLogger.LogDebug("Registered plugin command type {name}", commandType.FullName);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            foreach (var configurationType in configurations)
            {
                defaultLogger.LogDebug("Registered plugin config type {name}", configurationType.Name);
                var configInstance = (IBaseConfiguration) Activator.CreateInstance(configurationType);
                var handlerType = typeof(BaseConfigurationHandler<>).MakeGenericType(configurationType);
                var handlerInstance = Activator.CreateInstance(handlerType, new[] {configInstance.Name()});
                var genericInterfaceType = typeof(IConfigurationHandler<>).MakeGenericType(configurationType);
                
                serviceCollection.AddSingleton(genericInterfaceType, handlerInstance);
            }

            // register any script plugins
            foreach (var scriptPlugin in pluginImporter.DiscoverScriptPlugins())
            {
                serviceCollection.AddSingleton(scriptPlugin);
            }

            // register any eventable types
            foreach (var assemblyType in typeof(Program).Assembly.GetTypes()
                .Where(_asmType => typeof(IRegisterEvent).IsAssignableFrom(_asmType))
                .Union(plugins.SelectMany(_asm => _asm.Assembly.GetTypes())
                    .Distinct()
                    .Where(_asmType => typeof(IRegisterEvent).IsAssignableFrom(_asmType))))
            {
                var instance = Activator.CreateInstance(assemblyType) as IRegisterEvent;
                serviceCollection.AddSingleton(instance);
            }

            return serviceCollection;
        }


        /// <summary>
        /// Configures the dependency injection services
        /// </summary>
        private static IServiceCollection ConfigureServices(string[] args)
        {
            // setup the static resources (config/master api/translations)
            var serviceCollection = new ServiceCollection();
            var appConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
            var defaultConfigHandler = new BaseConfigurationHandler<DefaultSettings>("DefaultSettings");
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
                appConfigHandler.Save().RunSynchronously();
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
                .AddSingleton<IServiceCollection>(_serviceProvider => serviceCollection)
                .AddSingleton<IConfigurationHandler<DefaultSettings>, BaseConfigurationHandler<DefaultSettings>>()
                .AddSingleton((IConfigurationHandler<ApplicationConfiguration>) appConfigHandler)
                .AddSingleton(
                    new BaseConfigurationHandler<CommandConfiguration>("CommandConfiguration") as
                        IConfigurationHandler<CommandConfiguration>)
                .AddSingleton(appConfig)
                .AddSingleton(_serviceProvider =>
                    _serviceProvider.GetRequiredService<IConfigurationHandler<CommandConfiguration>>()
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
