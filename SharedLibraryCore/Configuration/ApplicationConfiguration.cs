using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedLibraryCore.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {
        public bool EnableWebFront { get; set; }
        public bool EnableMultipleOwners { get; set; }
        public bool EnableSteppedHierarchy { get; set; }
        public bool EnableClientVPNs { get; set; }
        public bool EnableDiscordLink { get; set; }
        public bool EnableCustomSayName { get; set; }
        public string CustomSayName { get; set; }
        public string DiscordInviteCode { get; set; }
        public string IPHubAPIKey { get; set; }
        public string WebfrontBindUrl { get; set; }
        public string Id { get; set; }
        public List<ServerConfiguration> Servers { get; set; }
        public int AutoMessagePeriod { get; set; }
        public List<string> AutoMessages { get; set; }
        public List<string> GlobalRules { get; set; }
        public List<MapConfiguration> Maps { get; set; }

        public IBaseConfiguration Generate()
        {
            Id = Guid.NewGuid().ToString();
            EnableWebFront = Utilities.PromptBool("Enable webfront");
            EnableMultipleOwners = Utilities.PromptBool("Enable multiple owners");
            EnableSteppedHierarchy = Utilities.PromptBool("Enable stepped privilege hierarchy");
            EnableCustomSayName = Utilities.PromptBool("Enable custom say name");

            WebfrontBindUrl = "http://127.0.0.1:1624";

            if (EnableCustomSayName)
                CustomSayName = Utilities.PromptString("Enter custom say name");

            EnableClientVPNs = Utilities.PromptBool("Enable client VPNS");

            if (!EnableClientVPNs)
                IPHubAPIKey = Utilities.PromptString("Enter iphub.info api key");

            EnableDiscordLink = Utilities.PromptBool("Display discord link on webfront");

            if (EnableDiscordLink)
                DiscordInviteCode = Utilities.PromptString("Enter discord invite link");

            return this;
        }

        public string Name() => "ApplicationConfiguration";
    }
}