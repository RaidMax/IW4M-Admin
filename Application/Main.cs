using System;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

using SharedLibraryCore;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Database;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using SharedLibraryCore.Localization;
using IW4MAdmin.Application.Migration;

namespace IW4MAdmin.Application
{
    public class Program
    {
        static public double Version { get; private set; }
        static public ApplicationManager ServerManager;
        private static ManualResetEventSlim OnShutdownComplete = new ManualResetEventSlim();

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", Utilities.OperatingDirectory);

            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Gray;

            Version = Utilities.GetVersionAsDouble();

            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine($" Version {Utilities.GetVersionAsString()}");
            Console.WriteLine("=====================================================");

            Index loc = null;

            try
            {
                ServerManager = ApplicationManager.GetInstance();
                Localization.Configure.Initialize(ServerManager.GetApplicationSettings().Configuration()?.CustomLocale);
                loc = Utilities.CurrentLocalization.LocalizationIndex;
                Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKey);

                CheckDirectories();
                // do any needed migrations
                // todo: move out
                ConfigurationMigration.MoveConfigFolder10518(null);

                ServerManager.Logger.WriteInfo($"Version is {Version}");

                var api = API.Master.Endpoint.Get();

                var version = new API.Master.VersionInfo()
                {
                    CurrentVersionStable = 99.99f
                };

                try
                {
                    version = api.GetVersion().Result;
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
                    Console.WriteLine($"{loc["MANAGER_VERSION_CURRENT"]} [v{Version.ToString("0.0")}]");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
#else
                else if (version.CurrentVersionPrerelease > Version)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"IW4MAdmin-Prerelease {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionPrerelease.ToString("0.0")}-pr]");
                    Console.WriteLine($"{loc["MANAGER_VERSION_CURRENT"]} [v{Version.ToString("0.0")}-pr]");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
#endif 
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(loc["MANAGER_VERSION_SUCCESS"]);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                ServerManager.Init().Wait();

                var consoleTask = Task.Run(async () =>
                {
                    String userInput;
                    var Origin = Utilities.IW4MAdminClient(ServerManager.Servers[0]);

                    do
                    {
                        userInput = Console.ReadLine();

                        if (userInput?.ToLower() == "quit")
                            ServerManager.Stop();

                        if (ServerManager.Servers.Count == 0)
                        {
                            Console.WriteLine(loc["MANAGER_CONSOLE_NOSERV"]);
                            continue;
                        }

                        if (userInput?.Length > 0)
                        {
                            GameEvent E = new GameEvent()
                            {
                                Type = GameEvent.EventType.Command,
                                Data = userInput,
                                Origin = Origin,
                                Owner = ServerManager.Servers[0]
                            };

                            ServerManager.GetEventHandler().AddEvent(E);
                            await E.WaitAsync(30 * 1000);
                        }
                        Console.Write('>');

                    } while (ServerManager.Running);
                });
            }

            catch (Exception e)
            {
                Console.WriteLine(loc["MANAGER_INIT_FAIL"]);
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Console.WriteLine(e.Message);
                Console.WriteLine(loc["MANAGER_EXIT"]);
                Console.ReadKey();
                return;
            }

            if (ServerManager.GetApplicationSettings().Configuration().EnableWebFront)
            {
                Task.Run(() => WebfrontCore.Program.Init(ServerManager));
            }

            OnShutdownComplete.Reset();
            ServerManager.Start();
            ServerManager.Logger.WriteVerbose(loc["MANAGER_SHUTDOWN_SUCCESS"]);
            OnShutdownComplete.Set();
        }

        private static void OnCancelKey(object sender, ConsoleCancelEventArgs e)
        {
            ServerManager.Stop();
            OnShutdownComplete.Wait();
        }

        static void CheckDirectories()
        {
            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Plugins")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Plugins"));
            }

            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Database")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Database"));
            }

            if (!Directory.Exists(Path.Join(Utilities.OperatingDirectory, "Log")))
            {
                Directory.CreateDirectory(Path.Join(Utilities.OperatingDirectory, "Log"));
            }
        }
    }
}
