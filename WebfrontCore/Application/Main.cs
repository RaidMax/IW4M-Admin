
using System;
using System.Runtime.InteropServices;
using SharedLibrary;
using System.Threading.Tasks;
using System.IO;
using SharedLibrary.Objects;
using System.Reflection;
using System.Linq;

namespace IW4MAdmin
{
    public class Program
    {
        [DllImport("kernel32.dll")]
        public static extern bool AllocConsole();
        static public double Version { get; private set; }
        static public ApplicationManager ServerManager = ApplicationManager.GetInstance();
        public static string OperatingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;

        public static void Start()
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", OperatingDirectory);
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;

            Version = 1.6;

            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine($" Version {Version}");
            Console.WriteLine("=====================================================");

            try
            {
                CheckDirectories();

                Task.Run(async () =>
               {
                   ServerManager = ApplicationManager.GetInstance();
                   SharedLibrary.Database.Repair.Run(ServerManager.Logger);
                   await ServerManager.Init();
                   ServerManager.Start();
               });

                Task.Run(() =>
                {
                    String userInput;
                    Player Origin = ServerManager.GetClientService().Get(1).Result.AsPlayer();

                    do
                    {
                        userInput = Console.ReadLine();

                        if (userInput?.ToLower() == "quit")
                            ServerManager.Stop();

                        if (ServerManager.Servers.Count == 0)
                            return;

                        Origin.CurrentServer = ServerManager.Servers[0];
                        Event E = new Event(Event.GType.Say, userInput, Origin, null, ServerManager.Servers[0]);
                        ServerManager.Servers[0].ExecuteEvent(E);
                        Console.Write('>');

                    } while (ServerManager.Running);

                    Console.WriteLine("Shutdown complete");
                });

            }

            catch (Exception e)
            {
                Console.WriteLine($"Fatal Error during initialization: {e.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
        }

        static void CheckDirectories()
        {
            string curDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;

            if (!Directory.Exists($"{curDirectory}Config"))
            {
                Console.WriteLine("Warning: Config folder does not exist");
                Directory.CreateDirectory($"{curDirectory}Config");
            }

            if (!Directory.Exists($"{curDirectory}Config/Servers"))
                Directory.CreateDirectory($"{curDirectory}Config/Servers");

            if (!Directory.Exists($"{curDirectory}Logs"))
                Directory.CreateDirectory($"{curDirectory}Logs");

            if (!Directory.Exists($"{curDirectory}Database"))
                Directory.CreateDirectory($"{curDirectory}Database");

            if (!Directory.Exists($"{curDirectory}Plugins"))
                Directory.CreateDirectory($"{curDirectory}Plugins");
        }
    }
}
