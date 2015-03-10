using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace IW4MAdmin
{
    class Program
    {
        static String IP;
        static int Port;
        static String RCON;
        static public double Version = 0.2;
        static public double latestVersion;

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
 

            file Config = new file("config\\servers.cfg");
            String[] SV_CONF = Config.readAll();

            if (SV_CONF == null || SV_CONF.Length < 1)
            {
                setupConfig();
                SV_CONF = new file("config\\servers.cfg").getParameters(3);
            }


            if (Config.getSize() > 0 && SV_CONF != null)
            {
                foreach (String S in SV_CONF)
                {
                    if (S.Length < 1)
                        continue;

                    String[] sv = S.Split(':');

                    Console.WriteLine("Starting admin on port " + sv[1]);

                    Server IW4M;
                    IW4M = new Server(sv[0], Convert.ToInt32(sv[1]), sv[2]);

                    //Threading seems best here
                    Thread monitorThread = new Thread(new ThreadStart(IW4M.Monitor));
                    monitorThread.Start();
                }
                
            }
            else
            {
                Console.WriteLine("[FATAL] CONFIG FILE DOES NOT EXIST OR IS INCORRECT!");
                Utilities.Wait(5);
            }
                
        }

        static void setupConfig()
        {
            Console.WriteLine("Hey there, it looks like you haven't set up a server yet. Let's get started!");
            Console.Write("Please enter the IP: ");
            IP = Console.ReadLine();
            Console.Write("Please enter the Port: ");
            Port = Convert.ToInt32(Console.ReadLine());
            Console.Write("Please enter the RCON password: ");
            RCON = Console.ReadLine();
            file Config = new file("config\\servers.cfg", true);
            Config.Write(IP + ":" + Port + ":" + RCON);
            Console.WriteLine("Great! Let's go ahead and start 'er up.");
        }

        static String checkUpdate()
        {
            Connection Ver = new Connection("http://raidmax.org/IW4M/Admin/version.php");
            return Ver.Read();
        }
    }
}
