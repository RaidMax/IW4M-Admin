using System;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

using SharedLibraryCore;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Database;
using System.Text;
using System.Threading;

namespace IW4MAdmin.Application
{
    public class Program
    {
        static public double Version { get; private set; }
        static public ApplicationManager ServerManager = ApplicationManager.GetInstance();
        public static string OperatingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", OperatingDirectory);
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
            Localization.Configure.Initialize();
            var loc = Utilities.CurrentLocalization.LocalizationSet;
            Console.OutputEncoding = Encoding.UTF8;

            Version = Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;
            Version = Math.Round(Version, 2);

            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine($" Version {Version.ToString("0.0")}");
            Console.WriteLine("=====================================================");

            try
            {
                CheckDirectories();

                ServerManager = ApplicationManager.GetInstance();

                 using (var db = new DatabaseContext(ServerManager.GetApplicationSettings().Configuration().ConnectionString))
                    new ContextSeed(db).Seed().Wait();

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
                    Console.ForegroundColor = ConsoleColor.White;
                }

#if !PRERELEASE
                else if (version.CurrentVersionStable > Version)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"IW4MAdmin {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionStable.ToString("0.0")}]");
                    Console.WriteLine($"{loc["MANAGER_VERSION_CURRENT"]} [v{Version.ToString("0.0")}]");
                    Console.ForegroundColor = ConsoleColor.White;
                }
#else
                else if (version.CurrentVersionPrerelease > Version)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"IW4MAdmin-Prerelease {loc["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionPrerelease.ToString("0.0")}-pr]");
                    Console.WriteLine($"{loc["MANAGER_VERSION_CURRENT"]} [v{Version.ToString("0.0")}-pr]");
                    Console.ForegroundColor = ConsoleColor.White;
                }
#endif 
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(loc["MANAGER_VERSION_SUCCESS"]);
                    Console.ForegroundColor = ConsoleColor.White;
                }

                ServerManager.Init().Wait();

                var consoleTask = Task.Run(() =>
                {
                    String userInput;
                    Player Origin = ServerManager.GetClientService().Get(1).Result.AsPlayer();

                    do
                    {
                        userInput = Console.ReadLine();

                        if (userInput?.ToLower() == "quit")
                            ServerManager.Stop();

                        if (ServerManager.Servers.Count == 0)
                        {
                            Console.WriteLine("No servers are currently being monitored");
                            continue;
                        }

                        Origin.CurrentServer = ServerManager.Servers[0];
                        GameEvent E = new GameEvent(GameEvent.EventType.Say, userInput, Origin, null, ServerManager.Servers[0]);
                        ServerManager.Servers[0].ExecuteEvent(E);
                        Console.Write('>');

                    } while (ServerManager.Running);
                });

                if (ServerManager.GetApplicationSettings().Configuration().EnableWebFront)
                {
                    Task.Run(() => WebfrontCore.Program.Init(ServerManager));
                }

                ServerManager.Start();
                ServerManager.Logger.WriteVerbose(loc["MANAGER_SHUTDOWN_SUCCESS"]);

            }

            catch (Exception e)
            {
                Console.WriteLine(loc["MANAGER_INIT_FAIL"]);
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Console.WriteLine($"Exception: {e.Message}");
                Console.WriteLine(loc["MANAGER_EXIT"]);
                Console.ReadKey();
            }
        }

        static void CheckDirectories()
        {
            string curDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;

            if (!Directory.Exists($"{curDirectory}Plugins"))
                Directory.CreateDirectory($"{curDirectory}Plugins");
        }
    }
}
