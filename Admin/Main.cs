
#define USINGMEMORY
using System;
using System.Runtime.InteropServices;
using SharedLibrary;
using System.Threading.Tasks;
using System.IO;

namespace IW4MAdmin
{
    class Program
    {
        static public double Version { get; private set; }
        static private Manager ServerManager;

        static void Main(string[] args)
        {
            Version = 1.3;
            handler = new ConsoleEventDelegate(OnProcessExit);
            SetConsoleCtrlHandler(handler, true);

            double.TryParse(CheckUpdate(), out double latestVersion);
            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            if (latestVersion != 0)
                Console.WriteLine(" Version " + Version + " (latest " + latestVersion + ")");
            else
                Console.WriteLine(" Version " + Version + " (unable to retrieve latest)");
            Console.WriteLine("=====================================================");

            try
            {
                CheckDirectories();

                ServerManager = Manager.GetInstance();
                ServerManager.Init();

                Task.Run(() =>
                {
                    String userInput;
                    Player Origin = new Player("IW4MAdmin", "", -1, Player.Permission.Console, -1, "", 0, "");

                    do
                    {
                        userInput = Console.ReadLine();

                        if (userInput.ToLower() == "quit")
                            ServerManager.Stop();

                        if (ServerManager.Servers.Count == 0)
                            return;

                        Event E = new Event(Event.GType.Say, userInput, Origin, null, ServerManager.Servers[0]);
                        Origin.lastEvent = E;
                        ServerManager.Servers[0].ExecuteEvent(E);
                        Console.Write('>');

                    } while (ServerManager.Running);
                });

            }

            catch(Exception e)
            {
                Console.WriteLine($"Fatal Error during initialization: {e.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            ServerManager.Start();
        }

        static ConsoleEventDelegate handler;

        static private bool OnProcessExit(int e)
        {
            try
            {
                ServerManager.Stop();
                return true;
            }

            catch
            {
                return true;
            }
        }

        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        static private String CheckUpdate()
        {
            //Connection Ver = new Connection("http://raidmax.org/IW4M/Admin/version.php");
            //return Ver.Read();
            return "0";
        }

        static void CheckDirectories()
        {
            if (!Directory.Exists("Lib"))
                throw new Exception("Lib folder does not exist");

            if (!Directory.Exists("Config"))
            {
                Console.WriteLine("Warning: Config folder does not exist");
                Directory.CreateDirectory("Config");
            }

            if (!Directory.Exists("Config/Servers"))
                Directory.CreateDirectory("Config/Servers");

            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            if (!Directory.Exists("Database"))
                Directory.CreateDirectory("Database");

            if (!Directory.Exists("Plugins"))
                Directory.CreateDirectory("Plugins");
        }
    }
}
