
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
        static public double Version { get; private set; }
        static public ApplicationManager ServerManager = ApplicationManager.GetInstance();
        public static string OperatingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;

        public static bool Start()
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
                /*var v1 = SharedLibrary.Helpers.Vector3.Parse("(737, 1117, 268)");
                var v2 = SharedLibrary.Helpers.Vector3.Parse("(1510, 672.98, -228.66)");
                double angleBetween = v1.AngleBetween(v2);*/
                CheckDirectories();

                ServerManager = ApplicationManager.GetInstance();
                SharedLibrary.Database.Repair.Run(ServerManager.Logger);
                ServerManager.Init().Wait();
                Task.Run(() => ServerManager.Start());

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

                return true;

            }

            catch (Exception e)
            {
                Console.WriteLine($"Fatal Error during initialization: {e.Message}");
                while(e.InnerException != null)
                {
                    e = e.InnerException;
                    Console.WriteLine($"Inner exception: {e.Message}");
                }
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return false;
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
