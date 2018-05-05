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
        public string CustomParserEncoding { get; set; }
        public string CustomLocale { get; set; }
        public string ConnectionString { get; set; }
        public string Id { get; set; }
        public List<ServerConfiguration> Servers { get; set; }
        public int AutoMessagePeriod { get; set; }
        public List<string> AutoMessages { get; set; }
        public List<string> GlobalRules { get; set; }
        public List<MapConfiguration> Maps { get; set; }

        public IBaseConfiguration Generate()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            Id = Guid.NewGuid().ToString();
            
            EnableWebFront = Utilities.PromptBool(loc["SETUP_ENABLE_WEBFRONT"]);
            EnableMultipleOwners = Utilities.PromptBool(loc["SETUP_ENABLE_MULTIOWN"]);
            EnableSteppedHierarchy = Utilities.PromptBool(loc["SETUP_ENABLE_STEPPEDPRIV"]);
            EnableCustomSayName = Utilities.PromptBool(loc["SETUP_ENABLE_CUSTOMSAY"]);

            bool useCustomParserEncoding = Utilities.PromptBool(loc["SETUP_USE_CUSTOMENCODING"]);
            CustomParserEncoding = useCustomParserEncoding ? Utilities.PromptString(loc["SETUP_ENCODING_STRING"]) : "windows-1252";


            WebfrontBindUrl = "http://127.0.0.1:1624";

            if (EnableCustomSayName)
                CustomSayName = Utilities.PromptString(loc["SETUP_SAY_NAME"]);

            EnableClientVPNs = Utilities.PromptBool(loc["SETUP_ENABLE_VPNS"]);

            if (!EnableClientVPNs)
                IPHubAPIKey = Utilities.PromptString(loc["SETUP_IPHUB_KEY"]);

            EnableDiscordLink = Utilities.PromptBool(loc["SETUP_DISPLAY_DISCORD"]);

            if (EnableDiscordLink)
                DiscordInviteCode = Utilities.PromptString(loc["SETUP_DISCORD_INVITE"]);

            return this;
        }

        public string Name() => "ApplicationConfiguration";
    }
}