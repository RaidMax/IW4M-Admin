#define USINGMEMORY
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using SharedLibrary;

namespace IW4MAdmin
{
    class Program
    {
        static public double Version { get; private set; }
        static private Manager serverManager;

        static void Main(string[] args)
        {
            Version = 1.1;
            double latestVersion = 0;
            handler = new ConsoleEventDelegate(OnProcessExit);
            SetConsoleCtrlHandler(handler, true);

            double.TryParse(checkUpdate(), out latestVersion);
            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            if (latestVersion != 0)
                Console.WriteLine(" Version " + Version + " (latest " + latestVersion + ")");
            else
                 Console.WriteLine(" Version " + Version + " (unable to retrieve latest)");
            Console.WriteLine("=====================================================");
 
            serverManager = new Manager();

            Thread serverMGRThread = new Thread(serverManager.Init);
            serverMGRThread.Name = "Server Manager thread";
            serverMGRThread.Start();

            while(!serverManager.isReady())
            {
                SharedLibrary.Utilities.Wait(1);
            }

            if (serverManager.getServers() != null)
                getManager().mainLog.Write("IW4M Now Initialized!", Log.Level.Production);

            String userInput;
            Server serverToExecuteOn = serverManager.getServers()[0];
            Player Origin = new Player("IW4MAdmin", "", -1, Player.Permission.Console, -1, "", 0, "");

            do
            {
                userInput = Console.ReadLine();
                Event E = new Event(Event.GType.Say, userInput, Origin, null, serverToExecuteOn);
                Origin.lastEvent = E;
                serverToExecuteOn.processEvent(E);
                Console.Write('>');

            } while (userInput != null && serverManager.isRunning());

            serverMGRThread.Join();
            serverManager.mainLog.Write("Shutting down IW4MAdmin...", Log.Level.Debug);
        }

        static ConsoleEventDelegate handler;

        static private bool OnProcessExit(int e)
        {
            try
            {
                foreach (Server S in getServers())
                {
                    if (S == null)
                        continue;

                    S.Broadcast("^5IW4MAdmin ^7is going ^1offline^7");
                    S.isRunning = false;

                    if (Utilities.shutdownInterface(S.pID()))
                        getManager().mainLog.Write("Successfully removed IW4MAdmin from server with PID " + S.pID(), Log.Level.Debug);
                    else
                        getManager().mainLog.Write("Could not remove IW4MAdmin from server with PID " + S.pID(), Log.Level.Debug);
                }

                getManager().shutDown();
                return false;
            }

            catch
            {
                return true;
            }
        }

        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        static private String checkUpdate()
        {
            Connection Ver = new Connection("http://raidmax.org/IW4M/Admin/version.php");
            return Ver.Read();
        }

        static public Server[] getServers()
        {
            return serverManager.getServers().ToArray();
        }

        static public Manager getManager()
        {
            return serverManager;
        }
    }
}
