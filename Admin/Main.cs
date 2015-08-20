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
        static public double Version = 0.92;
        static public double latestVersion;
        static public bool usingMemory = true;
        static private Manager serverManager;
        static private IW4MAdmin_Web.WebFront frontEnd;

        static void Main(string[] args)
        {
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
 
            serverManager = new IW4MAdmin.Manager();

            Thread serverMGRThread = new Thread(serverManager.Init);
            serverMGRThread.Name = "Server Manager thread";
            serverMGRThread.Start();

            while(!serverManager.isReady())
            {
                SharedLibrary.Utilities.Wait(1);
            }

            if (serverManager.getServers() != null)
                Program.getManager().mainLog.Write("IW4M Now Initialized! Visit http://127.0.0.1:1624 for server overview.", Log.Level.Production);

            if (serverManager.getServers().Count > 0)
            {
                frontEnd = new IW4MAdmin_Web.WebFront();
                frontEnd.Init();
            }

            serverMGRThread.Join();
            serverManager.mainLog.Write("Shutting down IW4MAdmin...", Log.Level.Debug);
        }

        static ConsoleEventDelegate handler;

        static private bool OnProcessExit(int e)
        {
            try
            {
                foreach (Server S in IW4MAdmin.Program.getServers())
                {
                    if (S == null)
                        continue;

                    S.isRunning = false;

                    if (Utilities.shutdownInterface(S.pID()))
                        Program.getManager().mainLog.Write("Successfully removed IW4MAdmin from server with PID " + S.pID(), Log.Level.Debug);
                    else
                        Program.getManager().mainLog.Write("Could not remove IW4MAdmin from server with PID " + S.pID(), Log.Level.Debug);
                }

                Program.getManager().shutDown();
                frontEnd.webSchedule.Stop();
                frontEnd.webSchedule.Dispose();
            }

            catch
            {
                return true;
            }

            return false;
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
