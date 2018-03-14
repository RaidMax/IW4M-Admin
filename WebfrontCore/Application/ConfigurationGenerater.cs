using SharedLibrary;
using SharedLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin
{
    class ConfigurationGenerator
    {
        public static List<ServerConfiguration> GenerateServerConfig(List<ServerConfiguration> configList)
        {

            var newConfig = new ServerConfiguration();

            while (string.IsNullOrEmpty(newConfig.IPAddress))
            {
                try
                {
                    Console.Write("Enter server IP Address: ");
                    string input = Console.ReadLine();
                    IPAddress.Parse(input);
                    newConfig.IPAddress = input;
                }

                catch (Exception)
                {
                    continue;
                }
            }

            while (newConfig.Port == 0)
            {
                try
                {
                    Console.Write("Enter server port: ");
                    newConfig.Port = Int16.Parse(Console.ReadLine());
                }

                catch (Exception)
                {
                    continue;
                }

            }

            Console.Write("Enter server RCON password: ");
            newConfig.Password = Console.ReadLine();

            configList.Add(newConfig);

            Console.Write("Configuration saved, add another? [y/n]:");
            if (Console.ReadLine().ToLower().First() == 'y')
                GenerateServerConfig(configList);

            return configList;
        }

        public static ApplicationConfiguration GenerateApplicationConfig()
        {
            var config = new ApplicationConfiguration();

            Console.Write("Enable multiple owners? [y/n]: ");
            config.EnableMultipleOwners = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Enable trusted rank? [y/n]: ");
            config.EnableTrustedRank = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Enable server-side anti-cheat [y/n]: ");
            config.EnableAntiCheat = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Enable client VPNS [y/n]: ");
            config.EnableClientVPNs = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            if (!config.EnableClientVPNs)
            {
                Console.Write("Enter iphub.info api key: ");
                config.IPHubAPIKey = Console.ReadLine();
            }

            Console.Write("Display Discord link on webfront [y/n]: ");
            config.EnableDiscordLink = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            if (config.EnableDiscordLink)
            {
                Console.Write("Enter Discord invite link: ");
                config.DiscordInviteCode = Console.ReadLine();
            }

            return config;
        }
    }
}
