using IW4MAdmin.Application.Migration;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    public class Program
    {
        public static double Version { get; private set; } = Utilities.GetVersionAsDouble();
        public static ApplicationManager ServerManager;
        private static Task ApplicationTask;

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
            try
            {
                ServerManager = ApplicationManager.GetInstance();
                var configuration = ServerManager.GetApplicationSettings().Configuration();
                Localization.Configure.Initialize(configuration?.EnableCustomLocale ?? false ? (configuration.CustomLocale ?? "en-US") : "en-US");

                // do any needed housekeeping file/folder migrations
                ConfigurationMigration.MoveConfigFolder10518(null);
                ConfigurationMigration.CheckDirectories();

                ServerManager.Logger.WriteInfo(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_VERSION"].FormatExt(Version));

                ConfigureServices();
                await CheckVersion();
                await ServerManager.Init();
            }

            catch (Exception e)
            {
                var loc = Utilities.CurrentLocalization.LocalizationIndex;
                string failMessage = loc == null ? "Failed to initalize IW4MAdmin" : loc["MANAGER_INIT_FAIL"];
                string exitMessage = loc == null ? "Press any key to exit..." : loc["MANAGER_EXIT"];

                Console.WriteLine(failMessage);

                while (e.InnerException != null)
                {
                    e = e.InnerException;
                }

                Console.WriteLine(e.Message);
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
        }

        /// <summary>
        /// runs the core application tasks
        /// </summary>
        /// <returns></returns>
        private static async Task RunApplicationTasksAsync()
        {
            var webfrontTask = ServerManager.GetApplicationSettings().Configuration().EnableWebFront ?
                WebfrontCore.Program.Init(ServerManager, ServerManager.CancellationToken) :
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
            inputThread.Abort();

            ServerManager.Logger.WriteVerbose(Utilities.CurrentLocalization.LocalizationIndex["MANAGER_SHUTDOWN_SUCCESS"]);
        }

        /// <summary>
        /// checks for latest version of the application
        /// notifies user if an update is available
        /// </summary>
        /// <returns></returns>
        private static async Task CheckVersion()
        {
            var api = API.Master.Endpoint.Get();
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            var version = new API.Master.VersionInfo()
            {
                CurrentVersionStable = 99.99f
            };

            try
            {
                version = await api.GetVersion();
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

            if (version.CurrentVersionStable == 99.99f)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(loc["MANAGER_VERSION_FAIL"]);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

#if !PRERELEASE
            else if (version.CurrentVersionStable > Version)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"IW4MAdmin {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionStable.ToString("0.0")}]");
                Console.WriteLine(loc["MANAGER_VERSION_CURRENT"].FormatExt($"[v{Version.ToString("0.0")}]"));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
#else
            else if (version.CurrentVersionPrerelease > Version)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"IW4MAdmin-Prerelease {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionPrerelease.ToString("0.0")}-pr]");
                Console.WriteLine(loc["MANAGER_VERSION_CURRENT"].FormatExt($"[v{Version.ToString("0.0")}-pr]"));
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

        private static void ConfigureServices()
        {
            var serviceProvider = new ServiceCollection();
            serviceProvider.AddSingleton<IManager>(ServerManager);
            var builder = serviceProvider.BuildServiceProvider();
            builder.Dispose();
        }
    }
}
