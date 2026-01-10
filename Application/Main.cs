using IW4MAdmin.Application.API.Master;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Application.Meta;
using IW4MAdmin.Application.Migration;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.DependencyInjection;
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
using IW4MAdmin.Application.Alerts;
using IW4MAdmin.Application.Extensions;
using IW4MAdmin.Application.IO;
using IW4MAdmin.Application.Localization;
using IW4MAdmin.Application.Plugin;
using IW4MAdmin.Application.Plugin.CSharpScript;
using IW4MAdmin.Application.Plugin.Script;
using IW4MAdmin.Application.QueryHelpers;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using IW4MAdmin.Plugins.Stats.Client.Abstractions;
using IW4MAdmin.Plugins.Stats.Client;
using Microsoft.Extensions.Hosting;
#if DEBUG
using PluginDebugReference;
#endif
using Refit;
using SharedLibraryCore.Interfaces.Events;
using Stats.Client.Abstractions;
using Stats.Client;
using Stats.Config;
using WebfrontCore.Core.QueryHelpers.Models;

namespace IW4MAdmin.Application
{
    public class Program
    {
        public static BuildNumber Version { get; } = BuildNumber.Parse(Utilities.GetVersionAsString());
        private static ApplicationManager _serverManager;
        private static Task _applicationTask;
        private static IServiceProvider _serviceProvider;

        private static readonly Lock Lock = new();
        private static bool _isExiting;

        // TODO: Temporary shim for Dragonfruit removal.
        public static async Task Main()
        {
            await Main(false, 25, 25);
        }

        /// <summary>
        /// entrypoint of the application
        /// </summary>
        /// <returns></returns>
        public static async Task Main(bool noConfirm = false, int? maxConcurrentRequests = 25,
            int? requestQueueLimit = 25)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", Utilities.OperatingDirectory);
            Directory.SetCurrentDirectory(Utilities.OperatingDirectory);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                var libraryName = eventArgs.Name.Split(",").First();

                var overrides = new[] { nameof(SharedLibraryCore), nameof(Stats) };
                if (!overrides.Contains(libraryName))
                {
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(asm => asm.FullName == eventArgs.Name);
                }

                // added to be a bit more permissive with plugin references
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(asm => asm.FullName?.StartsWith(libraryName) ?? false);
            };

            if (noConfirm)
            {
                AppContext.SetSwitch("NoConfirmPrompt", true);
            }

            Environment.SetEnvironmentVariable("MaxConcurrentRequests",
                (maxConcurrentRequests * Environment.ProcessorCount).ToString());
            Environment.SetEnvironmentVariable("RequestQueueLimit", requestQueueLimit.ToString());

            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.CancelKeyPress += OnCancelKey;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4MAdmin");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine($" Version {Utilities.GetVersionAsString()}");
            Console.WriteLine("=====================================================");

            Console.ForegroundColor = ConsoleColor.Gray;

            await LaunchAsync();
        }


        /// <summary>
        /// A single, thread-safe method to perform the application shutdown.
        /// It's designed to be idempotent (safe to call multiple times).
        /// </summary>
        private static async Task PerformShutdownAsync()
        {
            var shouldShutdown = false;

            lock (Lock)
            {
                if (!_isExiting)
                {
                    _isExiting = true;
                    shouldShutdown = true;
                }
            }

            if (!shouldShutdown)
            {
                return;
            }

            Utilities.DefaultLogger.LogInformation("Shutdown signal received. Stopping application manager...");

            if (_serverManager is not null)
            {
                await _serverManager.Stop();
            }

            if (_applicationTask is not null)
            {
                await _applicationTask;
            }
        }

        /// <summary>
        /// Handles Ctrl+C (SIGINT). We can prevent the process from exiting immediately
        /// to perform a graceful, asynchronous shutdown.
        /// </summary>
        private static async void OnCancelKey(object sender, ConsoleCancelEventArgs e)
        {
            Utilities.DefaultLogger.LogDebug(
                "SIGINT received (via Console.CancelKeyPress), performing asynchronous shutdown");
            // Prevent the OS from terminating the process, allowing our cleanup to run.
            e.Cancel = true;
            await PerformShutdownAsync();
        }

        /// <summary>
        /// A fallback handler for other process exit scenarios.
        /// The process is already terminating, so we must run cleanup synchronously.
        /// </summary>
        private static void OnProcessExit(object sender, EventArgs e)
        {
            Utilities.DefaultLogger.LogDebug("ProcessExit event received, performing synchronous shutdown");
            PerformShutdownAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// task that initializes application and starts the application monitoring and runtime tasks
        /// </summary>
        /// <returns></returns>
        private static async Task LaunchAsync()
        {
            restart:
            ITranslationLookup translationLookup = null;
            var logger = BuildDefaultLogger<Program>(new ApplicationConfiguration());
            Utilities.DefaultLogger = logger;
            logger.LogInformation("Begin IW4MAdmin startup. Version is {Version}", Version);

#if DEBUG
            StrongReferencesLoader.Load();
#endif

            try
            {
                // do any needed housekeeping file/folder migrations
                ConfigurationMigration.CheckDirectories();
                ConfigurationMigration.RemoveObsoletePlugins20210322();

                logger.LogDebug("Configuring services...");

                var configHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
                await configHandler.BuildAsync();
                var config = configHandler.Configuration() ?? new ApplicationConfiguration();
                _serviceProvider = WebfrontCore.Program.InitializeServices(ConfigureServices, config);
#pragma warning restore CS0618 // Type or member is obsolete

                _serverManager = (ApplicationManager)_serviceProvider.GetRequiredService<IManager>();
                translationLookup = _serviceProvider.GetRequiredService<ITranslationLookup>();

                await _serverManager.Init();

                // Start C# script plugin service host for hot reload support
                var csPluginHost = _serviceProvider.GetRequiredService<ICsPluginServiceHost>();
                await csPluginHost.StartAsync(_serverManager.CancellationToken);

                _applicationTask = RunApplicationTasksAsync(logger, _serverManager, _serviceProvider);

                await _applicationTask;
                logger.LogInformation("Shutdown completed successfully");
            }

            catch (Exception e)
            {
                var failMessage = translationLookup == null
                    ? "Failed to initialize IW4MAdmin"
                    : translationLookup["MANAGER_INIT_FAIL"];

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

                if (_serverManager is not null)
                {
                    await _serverManager.Stop();
                }

                return;
            }

            if (_serverManager.IsRestartRequested)
            {
                goto restart;
            }
        }

        /// <summary>
        /// runs the core application tasks
        /// </summary>
        /// <returns></returns>
        private static Task RunApplicationTasksAsync(ILogger logger, ApplicationManager applicationManager,
            IServiceProvider serviceProvider)
        {
            var collectionService = serviceProvider.GetRequiredService<IServerDataCollector>();
            var versionChecker = serviceProvider.GetRequiredService<IMasterCommunication>();
            var masterCommunicator = serviceProvider.GetRequiredService<IMasterCommunication>();
            var webfrontLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            using var onWebfrontErrored = new ManualResetEventSlim();

            var webfrontTask = _serverManager.GetApplicationSettings().Configuration().Webfront.Enabled
                ? WebfrontCore.Program.GetWebHostTask(_serverManager.CancellationToken).ContinueWith(continuation =>
                {
                    if (!continuation.IsFaulted)
                    {
                        return;
                    }

                    logger.LogCritical("Unable to start webfront task. {Message}",
                        continuation.Exception?.InnerException?.Message);

                    logger.LogDebug(continuation.Exception, "Unable to start webfront task");

                    onWebfrontErrored.Set();
                })
                : Task.CompletedTask;

            if (_serverManager.GetApplicationSettings().Configuration().Webfront.Enabled)
            {
                try
                {
                    onWebfrontErrored.Wait(webfrontLifetime.ApplicationStarted);
                }
                catch
                {
                    // ignored when webfront successfully starts
                }

                if (onWebfrontErrored.IsSet)
                {
                    return Task.CompletedTask;
                }
            }

            // we want to run this one on a manual thread instead of letting the thread pool handle it,
            // because we can't exit early from waiting on console input, and it prevents us from restarting
            async void ReadInput() => await ReadConsoleInput(logger);

            var inputThread = new Thread(ReadInput);
            inputThread.Start();

            var tasks = new[]
            {
                applicationManager.Start(),
                versionChecker.CheckVersion(),
                webfrontTask,
                masterCommunicator.RunUploadStatus(_serverManager.CancellationToken),
                collectionService.BeginCollectionAsync(cancellationToken: _serverManager.CancellationToken)
            };

            logger.LogDebug("Starting webfront and input tasks");
            return Task.WhenAll(tasks);
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

                    var readLineTask = Task.Run(() => Console.In.ReadLineAsync());
                    var completedTask = await Task.WhenAny(readLineTask,
                        Task.Delay(Timeout.Infinite, _serverManager.CancellationToken));
                    if (completedTask != readLineTask)
                    {
                        return;
                    }

                    var lastCommand = await readLineTask;
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
                         .Concat(typeof(Program).Assembly.GetTypes()
                             .Where(type => type.Namespace?.StartsWith("IW4MAdmin.Application.Commands") ?? false))
                         .Where(command => command.BaseType == typeof(Command)))
            {
                defaultLogger.LogDebug("Registered native command type {Name}", commandType.Name);
                serviceCollection.AddSingleton(typeof(IManagerCommand), commandType);
            }

            // register the plugin implementations
            var (plugins, commands, configurations) = pluginImporter.DiscoverAssemblyPluginImplementations();
            foreach (var pluginType in plugins)
            {
                var isV2 = pluginType.GetInterface(nameof(IPluginV2), false) != null;

                defaultLogger.LogDebug("Registering plugin type {Name}", pluginType.FullName);

                serviceCollection.AddSingleton(!isV2 ? typeof(IPlugin) : typeof(IPluginV2), pluginType);

                try
                {
                    var registrationMethod = pluginType.GetMethod(nameof(IPluginV2.RegisterDependencies));
                    registrationMethod?.Invoke(null, new object[] { serviceCollection });
                }
                catch (Exception ex)
                {
                    defaultLogger.LogError(ex, "Could not register plugin of type {Type}", pluginType.Name);
                }
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
                var configInstance = (IBaseConfiguration)Activator.CreateInstance(configurationType);
                var handlerType = typeof(BaseConfigurationHandler<>).MakeGenericType(configurationType);
                var handlerInstance = Activator.CreateInstance(handlerType, configInstance.Name());
                var genericInterfaceType = typeof(IConfigurationHandler<>).MakeGenericType(configurationType);

                serviceCollection.AddSingleton(genericInterfaceType, handlerInstance);
            }

            var scriptPlugins = pluginImporter.DiscoverScriptPlugins();

            foreach (var scriptPlugin in scriptPlugins)
            {
                serviceCollection.AddSingleton(scriptPlugin.Item1, sp =>
                    sp.GetRequiredService<IScriptPluginFactory>()
                        .CreateScriptPlugin(scriptPlugin.Item1, scriptPlugin.Item2));
            }

            // Pre-register .cs plugin dependencies before container is built
            // This allows .cs plugins to use RegisterDependencies just like DLL plugins
            var csPlugins = pluginImporter.DiscoverCsPlugins().ToList();
            if (csPlugins.Count > 0)
            {
                var compiler = new CsPluginCompiler(Utilities.DefaultLogger);

                foreach (var (_, filePath) in csPlugins)
                {
                    try
                    {
                        defaultLogger.LogDebug("Pre-compiling C# script plugin {FileName} for dependency registration",
                            Path.GetFileName(filePath));

                        var assembly = compiler.PreCompile(filePath);
                        var pluginType = assembly.GetTypes()
                            .FirstOrDefault(t =>
                                t is { IsInterface: false, IsAbstract: false } &&
                                t.GetInterface(nameof(IPluginV2)) != null);

                        if (pluginType == null)
                        {
                            continue;
                        }

                        // Invoke RegisterDependencies if present (same as DLL plugins)
                        var registrationMethod = pluginType.GetMethod(nameof(IPluginV2.RegisterDependencies));
                        if (registrationMethod == null)
                        {
                            continue;
                        }

                        defaultLogger.LogDebug("Invoking RegisterDependencies for {TypeName}", pluginType.Name);
                        registrationMethod.Invoke(null, [serviceCollection]);
                    }
                    catch (Exception ex)
                    {
                        defaultLogger.LogError(ex, "Failed to pre-register dependencies for .cs plugin {FileName}",
                            Path.GetFileName(filePath));
                    }
                }
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
        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // todo: this is a quick fix
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            serviceCollection.AddConfiguration<ApplicationConfiguration>("IW4MAdminSettings")
                .AddConfiguration<DefaultSettings>()
                .AddConfiguration<CommandConfiguration>()
                .AddConfiguration<StatsConfiguration>("StatsPluginSettings");

            // for legacy purposes. update at some point
            var appConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
            appConfigHandler.BuildAsync().GetAwaiter().GetResult();
            var commandConfigHandler = new BaseConfigurationHandler<CommandConfiguration>("CommandConfiguration");
            commandConfigHandler.BuildAsync().GetAwaiter().GetResult();

            if (appConfigHandler.Configuration()?.MasterUrl == new Uri("http://api.raidmax.org:5000"))
            {
                appConfigHandler.Configuration().MasterUrl = new ApplicationConfiguration().MasterUrl;
            }

            var appConfig = appConfigHandler.Configuration();
            var masterUri = Utilities.IsDevelopment
                ? new Uri("https://master.iw4.zip")
                : appConfig?.MasterUrl ?? new ApplicationConfiguration().MasterUrl;
            var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                BaseAddress = masterUri,
                Timeout = TimeSpan.FromSeconds(15)
            };
            var masterRestClient = RestService.For<IMasterApi>(httpClient);
            var translationLookup = Configure.Initialize(Utilities.DefaultLogger, masterRestClient, appConfig);

            if (appConfig == null)
            {
                appConfig = (ApplicationConfiguration)new ApplicationConfiguration().Generate();
                appConfigHandler.Set(appConfig);
                appConfigHandler.Save().GetAwaiter().GetResult();
            }

            // register override level names
            foreach (var (key, value) in appConfig.OverridePermissionLevelNames)
            {
                Utilities.PermissionLevelOverrides.TryAdd(key, value);
            }

            // build the dependency list
            serviceCollection
                .AddBaseLogger(appConfig)
                .AddSingleton((IConfigurationHandler<ApplicationConfiguration>)appConfigHandler)
                .AddSingleton<IConfigurationHandler<CommandConfiguration>>(commandConfigHandler)
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
#pragma warning disable CS0618
                .AddSingleton<IMetaService, MetaService>()
#pragma warning restore CS0618
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
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, ConnectionHistoryResponse>,
                    ConnectionsResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ClientPaginationRequest, PermissionLevelChangedResponse>,
                    PermissionLevelChangedResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse>,
                    ClientResourceQueryHelper>()
                .AddSingleton<IResourceQueryHelper<ChatSearchQuery, MessageResponse>, ChatResourceQueryHelper>()
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
                .AddSingleton<IGeoLocationService>(
                    new GeoLocationService(Path.Join(".", "Resources", "GeoLite2-Country.mmdb")))
                .AddSingleton<IAlertManager, AlertManager>()
#pragma warning disable CS0618
                .AddTransient<IScriptPluginTimerHelper, ScriptPluginTimerHelper>()
#pragma warning restore CS0618
                .AddSingleton<IInteractionRegistration, InteractionRegistration>()
                .AddSingleton<IRemoteCommandService, RemoteCommandService>()
                .AddSingleton(new ConfigurationWatcher())
                .AddSingleton(typeof(IConfigurationHandlerV2<>), typeof(BaseConfigurationHandlerV2<>))
                .AddSingleton<IScriptPluginFactory, ScriptPluginFactory>()
                .AddSingleton(new CsPluginCompiler(Utilities.DefaultLogger))
                .AddSingleton<ICsPluginServiceHost, CsPluginServiceHost>()
                .AddSingleton<IGameScriptEventFactory, GameScriptEventFactory>()
                .AddSingleton(translationLookup)
                .AddDatabaseContextOptions(appConfig);

            serviceCollection.AddSingleton<ICoreEventHandler, CoreEventHandler>();
            serviceCollection.AddSource();
            HandlePluginRegistration(appConfig, serviceCollection, masterRestClient);
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
