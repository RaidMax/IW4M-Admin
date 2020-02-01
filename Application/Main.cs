using IW4MAdmin.Application.Helpers;
using IW4MAdmin.Application.Migration;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    public class Program
    {
        public static BuildNumber Version { get; private set; } = BuildNumber.Parse(Utilities.GetVersionAsString());
        public static ApplicationManager ServerManager;
        private static Task ApplicationTask;
        private static readonly BuildNumber _fallbackVersion = BuildNumber.Parse("99.99.99.99");
        private static ServiceProvider serviceProvider;

        /// <summary>
        /// entrypoint of the application
        /// </summary>
        /// <returns></returns>
        public static async Task Main()
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

            await LaunchAsync();
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
            await ApplicationTask;
        }

        /// <summary>
        /// task that initializes application and starts the application monitoring and runtime tasks
        /// </summary>
        /// <returns></returns>
        private static async Task LaunchAsync()
        {
        restart:
            ITranslationLookup translationLookup = null;
            try
            {
                var services = ConfigureServices();
                serviceProvider = services.BuildServiceProvider();
                ServerManager = (ApplicationManager)serviceProvider.GetRequiredService<IManager>();
                translationLookup = serviceProvider.GetRequiredService<ITranslationLookup>();

                // do any needed housekeeping file/folder migrations
                ConfigurationMigration.MoveConfigFolder10518(null);
                ConfigurationMigration.CheckDirectories();

                ServerManager.Logger.WriteInfo(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_VERSION"].FormatExt(Version));

                await CheckVersion(translationLookup);
                await ServerManager.Init();
            }

            catch (Exception e)
            {
                string failMessage = translationLookup == null ? "Failed to initalize IW4MAdmin" : translationLookup["MANAGER_INIT_FAIL"];
                string exitMessage = translationLookup == null ? "Press any key to exit..." : translationLookup["MANAGER_EXIT"];

                Console.WriteLine(failMessage);

                while (e.InnerException != null)
                {
                    e = e.InnerException;
                }

                if (e is ConfigurationException configException)
                {
                    Console.WriteLine(translationLookup[configException.Message].FormatExt(configException.ConfigurationFileName));

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
                Console.ReadKey();
                return;
            }

            try
            {
                ApplicationTask = RunApplicationTasksAsync();
                await ApplicationTask;
            }

            catch { }

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
        private static async Task RunApplicationTasksAsync()
        {
            var webfrontTask = ServerManager.GetApplicationSettings().Configuration().EnableWebFront ?
                WebfrontCore.Program.Init(ServerManager, serviceProvider, ServerManager.CancellationToken) :
                Task.CompletedTask;

            // we want to run this one on a manual thread instead of letting the thread pool handle it,
            // because we can't exit early from waiting on console input, and it prevents us from restarting
            var inputThread = new Thread(async () => await ReadConsoleInput());
            inputThread.Start();

            var tasks = new[]
            {
                ServerManager.Start(),
                webfrontTask,
            };

            await Task.WhenAll(tasks);

            ServerManager.Logger.WriteVerbose(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_SHUTDOWN_SUCCESS"]);
        }

        /// <summary>
        /// checks for latest version of the application
        /// notifies user if an update is available
        /// </summary>
        /// <returns></returns>
        private static async Task CheckVersion(ITranslationLookup translationLookup)
        {
            var api = API.Master.Endpoint.Get();
            var loc = translationLookup;

            var version = new API.Master.VersionInfo()
            {
                CurrentVersionStable = _fallbackVersion
            };

            try
            {
                version = await api.GetVersion(1);
            }

            catch (Exception e)
            {
                ServerManager.Logger.WriteWarning(loc["MANAGER_VERSION_FAIL"]);
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                }

                ServerManager.Logger.WriteDebug(e.Message);
            }

            if (version.CurrentVersionStable == _fallbackVersion)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(loc["MANAGER_VERSION_FAIL"]);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

#if !PRERELEASE
            else if (version.CurrentVersionStable > Version)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"IW4MAdmin {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionStable.ToString()}]");
                Console.WriteLine(loc["MANAGER_VERSION_CURRENT"].FormatExt($"[v{Version.ToString()}]"));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
#else
            else if (version.CurrentVersionPrerelease > Version)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"IW4MAdmin-Prerelease {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionPrerelease.ToString()}-pr]");
                Console.WriteLine(loc["MANAGER_VERSION_CURRENT"].FormatExt($"[v{Version.ToString()}-pr]"));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
#endif
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(loc["MANAGER_VERSION_SUCCESS"]);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        /// <summary>
        /// reads input from the console and executes entered commands on the default server
        /// </summary>
        /// <returns></returns>
        private static async Task ReadConsoleInput()
        {
            string lastCommand;
            var Origin = Utilities.IW4MAdminClient(ServerManager.Servers[0]);

            try
            {
                while (!ServerManager.CancellationToken.IsCancellationRequested)
                {
                    lastCommand = Console.ReadLine();

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

                            ServerManager.GetEventHandler().AddEvent(E);
                            await E.WaitAsync(Utilities.DefaultCommandTimeout, ServerManager.CancellationToken);
                            Console.Write('>');
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            { }
        }

        /// <summary>
        /// Configures the dependency injection services
        /// </summary>
        private static IServiceCollection ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IServiceCollection>(_serviceProvider => serviceCollection)
                .AddSingleton(new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings") as IConfigurationHandler<ApplicationConfiguration>)
                .AddSingleton(new BaseConfigurationHandler<CommandConfiguration>("CommandConfiguration") as IConfigurationHandler<CommandConfiguration>)
                .AddSingleton(_serviceProvider => _serviceProvider.GetRequiredService<IConfigurationHandler<ApplicationConfiguration>>().Configuration())
                .AddSingleton(_serviceProvider => _serviceProvider.GetRequiredService<IConfigurationHandler<CommandConfiguration>>().Configuration() ?? new CommandConfiguration())
                .AddSingleton<ILogger>(_serviceProvider => new Logger("IW4MAdmin-Manager"))
                .AddSingleton<IPluginImporter, PluginImporter>()
                .AddSingleton<IMiddlewareActionHandler, MiddlewareActionHandler>()
                .AddTransient(_serviceProvider =>
                {
                    var importer = _serviceProvider.GetRequiredService<IPluginImporter>();
                    var config = _serviceProvider.GetRequiredService<CommandConfiguration>();
                    var layout = _serviceProvider.GetRequiredService<ITranslationLookup>();

                    // todo: this is disgusting, but I need it until I can figure out a way to dynamically load the plugins without creating an instance.
                    return importer.CommandTypes.
                        Union(typeof(SharedLibraryCore.Commands.QuitCommand).Assembly.GetTypes()
                        .Where(_command => _command.BaseType == typeof(Command)))
                        .Select(_cmdType => Activator.CreateInstance(_cmdType, config, layout) as IManagerCommand);
                })
                .AddSingleton(_serviceProvider =>
                {
                    var config = _serviceProvider.GetRequiredService<IConfigurationHandler<ApplicationConfiguration>>().Configuration();
                    return Localization.Configure.Initialize(useLocalTranslation: config?.UseLocalTranslations ?? false,
                        customLocale: config?.EnableCustomLocale ?? false ? (config.CustomLocale ?? "en-US") : "en-US");
                })
                .AddSingleton<IManager, ApplicationManager>();

            return serviceCollection;
        }
    }
}
