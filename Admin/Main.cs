#define USINGMEMORY
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace IW4MAdmin
{
    class Program
    {
        static public double Version = 0.91;
        static public double latestVersion;
        static public bool usingMemory = true;

        static void Main(string[] args)
        {
            double.TryParse(checkUpdate(), out latestVersion);
            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            if (latestVersion != 0)
                Console.WriteLine(" Version " + Version + " (latest " + latestVersion + ")");
            else
                 Console.WriteLine(" Version " + Version + " (unable to retrieve latest)");
            Console.WriteLine("=====================================================");
#if DEBUG           
            if (viableServers.Count < 1)
                viableServers = checkConfig(); // fall back to config    
            Servers = viableServers;

            foreach (Server IW4M in viableServers)
            {   
                //Threading seems best here
                Server SV = IW4M;
                Thread monitorThread = new Thread(new ThreadStart(SV.Monitor));
                monitorThread.Start();
            }
#endif  
            IW4MAdmin.Manager IW4MAdmin = new IW4MAdmin.Manager();
            IW4MAdmin.Init();

            Console.WriteLine("IW4M Now Initialized! Visit http://127.0.0.1:1624 for server overview.");

            IW4MAdmin_Web.WebFront frontEnd = new IW4MAdmin_Web.WebFront();
            frontEnd.Init();
        }
#if DEBUG
        static void setupConfig()
        {
            bool validPort = false;
            Console.WriteLine("Hey there, it looks like you haven't set up a server yet. Let's get started!");

            Console.Write("Please enter the IP: ");
            IP = Console.ReadLine();

            while (!validPort)
            {
                Console.Write("Please enter the Port: ");
                int.TryParse(Console.ReadLine(), out Port);
                if (Port != 0)
                    validPort = true;
            }

            Console.Write("Please enter the RCON password: ");
            RCON = Console.ReadLine();
            file Config = new file("config\\servers.cfg", true);
            Console.WriteLine("Great! Let's go ahead and start 'er up.");
        }
#endif

        static String checkUpdate()
        {
            Connection Ver = new Connection("http://raidmax.org/IW4M/Admin/version.php");
            return Ver.Read();
        }

#if DEBUG
        static List<Server> checkConfig()
        {

            file Config = new file("config\\servers.cfg");
            String[] SV_CONF = Config.readAll();
            List<Server> Servers = new List<Server>();
            Config.Close();

            if (SV_CONF == null || SV_CONF.Length < 1 || SV_CONF[0] == String.Empty)
            {
                setupConfig(); // get our first time server
                Config = new file("config\\servers.cfg", true);
                Config.Write(IP + ':' + Port + ':' + RCON);
                Config.Close();
                Servers.Add(new Server(IP, Port, RCON, 0));
            }

            else
            {
                foreach (String L in SV_CONF)
                {
                    String[] server_line = L.Split(':');
                    int newPort;
                    if (server_line.Length < 3 || !int.TryParse(server_line[1], out newPort))
                    {
                        Console.WriteLine("You have an error in your server.cfg");
                        continue;
                    }
                    Servers.Add(new Server(server_line[0], newPort, server_line[2],0));
                }
            }
            return Servers;
        }
#endif
    }
}
