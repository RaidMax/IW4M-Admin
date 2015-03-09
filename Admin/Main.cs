using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Program
    {
        static String IP;
        static int Port;
        static String RCON;


        static void Main(string[] args)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN v");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine("=====================================================");

            file Config = new file("config\\servers.cfg");
            String[] SV_CONF = Config.getParameters(3);
            double Version = 0.1;

            if (SV_CONF == null || SV_CONF.Length != 3)
            {
                setupConfig();
                SV_CONF = new file("config\\servers.cfg").getParameters(3);
            }


            if (Config.getSize() > 0 && SV_CONF != null)
            {
                Console.WriteLine("Starting admin on port " + SV_CONF[1]);

                Server IW4M;
                IW4M = new Server(SV_CONF[0], Convert.ToInt32(SV_CONF[1]), SV_CONF[2]);
                IW4M.Monitor();
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
    }
}
