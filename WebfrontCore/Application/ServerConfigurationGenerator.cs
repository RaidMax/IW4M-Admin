using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin
{
    class ServerConfigurationGenerator
    {
        public static ServerConfiguration Generate()
        {
            string IP = String.Empty;
            int Port = 0;
            string Password;
            bool AllowMultipleOwners;
            bool AllowTrustedRank;
            bool AntiCheat;
            bool AllowVpns;

            while (IP == String.Empty)
            {
                try
                {
                    Console.Write("Enter server IP: ");
                    string input = Console.ReadLine();
                    IPAddress.Parse(input);
                    IP = input;
                }

                catch (Exception)
                {
                    continue;
                }
            }

            while (Port == 0)
            {
                try
                {
                    Console.Write("Enter server port: ");
                    Port = Int32.Parse(Console.ReadLine());
                }

                catch (Exception)
                {
                    continue;
                }
            }

            Console.Write("Enter server RCON password: ");
            Password = Console.ReadLine();

            Console.Write("Allow multiple owners? [y/n]: ");
            AllowMultipleOwners = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Allow trusted rank? [y/n]: ");
            AllowTrustedRank = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Allow server-side anti-cheat [y/n]: ");
            AntiCheat = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Allow client VPNS [y/n]: ");
            AllowVpns = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            var config = new ServerConfiguration()
            {
                IP = IP,
                Password = Password,
                Port = Port,
                AllowMultipleOwners = AllowMultipleOwners,
                AllowTrustedRank = AllowTrustedRank,
                RestartPassword = "",
                RestartUsername = "",
                EnableAntiCheat = AntiCheat,
                AllowClientVpn = AllowVpns
            };

            config.Write();

            Console.Write("Configuration saved, add another? [y/n]:");
            if (Console.ReadLine().ToLower().First() == 'y')
                Generate();

            return config;
        }
    }
}
