using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Program
    {
        static void Main(string[] args)
        {

            file Config = new file("config\\servers.cfg");
            String[] SV_CONF = Config.getParameters(3);
            double Version = 0.1;

            if (Config.getSize() > 0 && SV_CONF != null)
            {
                Console.WriteLine("=====================================================");
                Console.WriteLine(" IW4M ADMIN v" + Version);
                Console.WriteLine(" by RaidMax ");
                Console.WriteLine("=====================================================");
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
    }
}
