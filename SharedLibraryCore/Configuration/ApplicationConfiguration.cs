using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SharedLibraryCore.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {
        [LocalizedDisplayName("SETUP_ENABLE_WEBFRONT")]
        public bool EnableWebFront { get; set; }
        [LocalizedDisplayName("SETUP_ENABLE_MULTIOWN")]
        public bool EnableMultipleOwners { get; set; }
        [LocalizedDisplayName("SETUP_ENABLE_STEPPEDPRIV")]
        public bool EnableSteppedHierarchy { get; set; }
        [LocalizedDisplayName("SETUP_DISPLAY_SOCIAL")]
        public bool EnableSocialLink { get; set; }
        [LocalizedDisplayName("SETUP_ENABLE_CUSTOMSAY")]
        public bool EnableCustomSayName { get; set; }
        [LocalizedDisplayName("SETUP_SAY_NAME")]
        public string CustomSayName { get; set; }
        [LocalizedDisplayName("SETUP_SOCIAL_LINK")]
        public string SocialLinkAddress { get; set; }
        [LocalizedDisplayName("SETUP_SOCIAL_TITLE")]
        public string SocialLinkTitle { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_BIND_URL")]
        public string WebfrontBindUrl { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MANUAL_URL")]
        public string ManualWebfrontUrl { get; set; }
        public string WebfrontUrl => string.IsNullOrEmpty(ManualWebfrontUrl) ? WebfrontBindUrl?.Replace("0.0.0.0", "127.0.0.1") : ManualWebfrontUrl;
        [LocalizedDisplayName("SETUP_USE_CUSTOMENCODING")]
        public bool EnableCustomParserEncoding { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENCODING")]
        public string CustomParserEncoding { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_LOCALE")]
        public bool EnableCustomLocale { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_LOCALE")]
        public string CustomLocale { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DB_PROVIDER")]
        public string DatabaseProvider { get; set; } = "sqlite";
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CONNECTION_STRING")]
        public string ConnectionString { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_RCON_POLLRATE")]
        public int RConPollRate { get; set; } = 5000;
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_IGNORE_BOTS")]
        public bool IgnoreBots { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MAX_TB")]
        public TimeSpan MaximumTempBanTime { get; set; } = new TimeSpan(24 * 30, 0, 0);
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENABLE_WHITELIST")]
        public bool EnableWebfrontConnectionWhitelist { get; set; }
        public List<string> WebfrontConnectionWhitelist { get; set; }
        public string Id { get; set; }
        public List<ServerConfiguration> Servers { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGE_PERIOD")]
        public int AutoMessagePeriod { get; set; }
        public List<string> AutoMessages { get; set; }
        public List<string> GlobalRules { get; set; }
        public List<MapConfiguration> Maps { get; set; }
        public List<QuickMessageConfiguration> QuickMessages { get; set; }
        public List<string> DisallowedClientNames { get; set; }

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

            WebfrontBindUrl = "http://0.0.0.0:1624";

            if (EnableCustomSayName)
                CustomSayName = Utilities.PromptString(loc["SETUP_SAY_NAME"]);

            EnableSocialLink = Utilities.PromptBool(loc["SETUP_DISPLAY_SOCIAL"]);

            if (EnableSocialLink)
            {
                SocialLinkTitle = Utilities.PromptString(loc["SETUP_SOCIAL_TITLE"]);
                SocialLinkAddress = Utilities.PromptString(loc["SETUP_SOCIAL_LINK"]);
            }

            RConPollRate = 5000;
            DisallowedClientNames = new List<string>()
            {
                "Unknown Soldier",
                "UnknownSoldier",
                "CHEATER",
                "VickNet"
            };

            return this;
        }

        public string Name() => "ApplicationConfiguration";
    }
}