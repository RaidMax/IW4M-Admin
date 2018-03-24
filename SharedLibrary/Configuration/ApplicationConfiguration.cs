using SharedLibrary.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedLibrary.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {
        public bool EnableMultipleOwners { get; set; }
        public bool EnableSteppedHierarchy { get; set; }
        public bool EnableClientVPNs { get; set; }
        public bool EnableDiscordLink { get; set; }
        public string DiscordInviteCode { get; set; }
        public string IPHubAPIKey { get; set; }
        public List<ServerConfiguration> Servers { get; set; }
        public int AutoMessagePeriod { get; set; }
        public List<string> AutoMessages { get; set; }
        public List<string> GlobalRules { get; set; }
        public List<MapConfiguration> Maps { get; set; }

        public IBaseConfiguration Generate()
        {
            Console.Write("Enable multiple owners? [y/n]: ");
            EnableMultipleOwners = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Enable stepped privilege hierarchy? [y/n]: ");
            EnableSteppedHierarchy = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            Console.Write("Enable client VPNs [y/n]: ");
            EnableClientVPNs = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            if (!EnableClientVPNs)
            {
                Console.Write("Enter iphub.info api key: ");
                IPHubAPIKey = Console.ReadLine();
            }

            Console.Write("Display discord link on webfront [y/n]: ");
            EnableDiscordLink = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            if (EnableDiscordLink)
            {
                Console.Write("Enter discord invite link: ");
                DiscordInviteCode = Console.ReadLine();
            }

            return this;
        }

        public string Name() => "ApplicationConfiguration";
    }
}