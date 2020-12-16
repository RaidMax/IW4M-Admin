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
using StatsWeb;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Application.Extensions;
using IW4MAdmin.Application.Localization;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application
{
    public class Program
    {
        public static BuildNumber Version { get; private set; } = BuildNumber.Parse(Utilities.GetVersionAsString());
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

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKey);

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
            logger.LogInformation("Begin IW4MAdmin startup. Version is {version} {@args}", Version, args);
            
            try
            {
                // do any needed housekeeping file/folder migrations
                ConfigurationMigration.MoveConfigFolder10518(null);
                ConfigurationMigration.CheckDirectories();
                logger.LogDebug("Configuring services...");
                services = ConfigureServices(args);
                serviceProvider = services.BuildServiceProvider();
                var versionChecker = serviceProvider.GetRequiredService<IMasterCommunication>();
                ServerManager = (ApplicationManager)serviceProvider.GetRequiredService<IManager>();
                translationLookup = serviceProvider.GetRequiredService<ITranslationLookup>();

                await versionChecker.CheckVersion();
                await ServerManager.Init();
            }

            catch (Exception e)
            {
                string failMessage = translationLookup == null ? "Failed to initialize IW4MAdmin" : translationLookup["MANAGER_INIT_FAIL"];
                string exitMessage = translationLookup == null ? "Press enter to exit..." : translationLookup["MANAGER_EXIT"];

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
                        Console.WriteLine(translationLookup[configException.Message].FormatExt(configException.ConfigurationFileName));
                    }

                    foreach (string error in configException.Errors)
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

            try
            {
                ApplicationTask = RunApplicationTasksAsync(logger, services);
                await ApplicationTask;
            }

            catch (Exception e)
            {
                logger.LogCritical(e, "Failed to launch IW4MAdmin");
                string failMessage = translationLookup == null ? "Failed to launch IW4MAdmin" : translationLookup["MANAGER_INIT_FAIL"];
                Console.WriteLine($"{failMessage}: {e.GetExceptionInfo()}");
            }

            if (ServerManager.IsRestartRequested)
            {
                goto restart;
            }

            serviceProvider.Dispose();
        }

        /// <summary>
        /// runs the core application tasks
        /// </summary>
        /// <returns></returns>
        private static async Task RunApplicationTasksAsync(ILogger logger, IServiceCollection services)
        {
            var webfrontTask = ServerManager.GetApplicationSettings().Configuration().EnableWebFront ?
                WebfrontCore.Program.Init(ServerManager, serviceProvider, services, ServerManager.CancellationToken) :
                Task.CompletedTask;

            // we want to run this one on a manual thread instead of letting the thread pool handle it,
            // because we can't exit early from waiting on console input, and it prevents us from restarting
            var inputThread = new Thread(async () => await ReadConsoleInput(logger));
            inputThread.Start();

            var tasks = new[]
            {
                ServerManager.Start(),
                webfrontTask,
                serviceProvider.GetRequiredService<IMasterCommunication>().RunUploadStatus(ServerManager.CancellationToken)
            };

            logger.LogDebug("Starting webfront and input tasks");
            await Task.WhenAll(tasks);

            logger.LogInformation("Shutdown completed successfully");
            Console.Write(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_SHUTDOWN_SUCCESS"]);
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
            var Origin = Utilities.IW4MAdminClient(ServerManager.Servers[0]);

            try
            {
                while (!ServerManager.CancellationToken.IsCancellationRequested)
                {
                    lastCommand = await Console.In.ReadLineAsync();

                    if (lastCommand?.Length > 0)
                    {
                        if (lastCommand?.Length > 0)
                        {
                            GameEvent E = new GameEvent()
                            {
                                Type = GameEvent.EventType.Command,
                                Data = lastCommand,
                                Origin = Origin,
                                Owner = ServerManager.Servers[0]
                            };

                            ServerManager.AddEvent(E);
                            await E.WaitAsync(Utilities.DefaultCommandTimeout, ServerManager.CancellationToken);
                            Console.Write('>');
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            { }
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
                        .Where(_command => _command.BaseType == typeof(Command)))
            {
                defaultLogger.LogDebug("Registered native command type {name}", commandType.Name);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            // register the plugin implementations
            var pluginImplementations = pluginImporter.DiscoverAssemblyPluginImplementations();
            foreach (var pluginType in pluginImplementations.Item1)
            {
                defaultLogger.LogDebug("Registered plugin type {name}", pluginType.FullName);
                serviceCollection.AddSingleton(typeof(IPlugin), pluginType);
            }

            // register the plugin commands
            foreach (var commandType in pluginImplementations.Item2)
            {
                defaultLogger.LogDebug("Registered plugin command type {name}", commandType.FullName);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            // register any script plugins
            foreach (var scriptPlugin in pluginImporter.DiscoverScriptPlugins())
            {
                serviceCollection.AddSingleton(scriptPlugin);
            }

            // register any eventable types
            foreach (var assemblyType in typeof(Program).Assembly.GetTypes()
                .Where(_asmType => typeof(IRegisterEvent).IsAssignableFrom(_asmType))
                .Union(pluginImplementations
                .Item1.SelectMany(_asm => _asm.Assembly.GetTypes())
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
            var appConfig = appConfigHandler.Configuration();
            var masterUri = Utilities.IsDevelopment
                ? new Uri("http://127.0.0.1:8080")
                : appConfig?.MasterUrl ?? new ApplicationConfiguration().MasterUrl;
            var masterRestClient = RestClient.For<IMasterApi>(masterUri);
            var translationLookup =  Configure.Initialize(Utilities.DefaultLogger, masterRestClient, appConfig);

            if (appConfig == null)
            {
                appConfig = (ApplicationConfiguration) new ApplicationConfiguration().Generate();
                appConfigHandler.Set(appConfig);
                appConfigHandler.Save();
            }
            
            // build the dependency list
            HandlePluginRegistration(appConfig, serviceCollection, masterRestClient);

            serviceCollection
                .AddBaseLogger(appConfig)
                .AddSingleton<IServiceCollection>(_serviceProvider => serviceCollection)
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
                .AddTransient<IParserPatternMatcher, ParserPatternMatcher>()
                .AddSingleton<IRemoteAssemblyHandler, RemoteAssemblyHandler>()
                .AddSingleton<IMasterCommunication, MasterCommunication>()
                .AddSingleton<IManager, ApplicationManager>()
                .AddSingleton<SharedLibraryCore.Interfaces.ILogger, Logger>()
                .AddSingleton<IClientNoticeMessageFormatter, ClientNoticeMessageFormatter>()
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
