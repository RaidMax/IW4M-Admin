using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SharedLibraryCore.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {

        [LocalizedDisplayName("SETUP_ENABLE_WEBFRONT")]
        [LinkedConfiguration("WebfrontBindUrl", "ManualWebfrontUrl")]
        public bool EnableWebFront { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_BIND_URL")]
        public string WebfrontBindUrl { get; set; }
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MANUAL_URL")]
        public string ManualWebfrontUrl { get; set; }

        [LocalizedDisplayName("SETUP_ENABLE_MULTIOWN")]
        public bool EnableMultipleOwners { get; set; }
        [LocalizedDisplayName("SETUP_ENABLE_STEPPEDPRIV")]
        public bool EnableSteppedHierarchy { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_USE_LOCAL_TRANSLATIONS")]
        public bool UseLocalTranslations { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_IGNORE_BOTS")]
        public bool IgnoreBots { get; set; }

        [LinkedConfiguration("CustomSayName")]
        [LocalizedDisplayName("SETUP_ENABLE_CUSTOMSAY")]
        public bool EnableCustomSayName { get; set; }
        [LocalizedDisplayName("SETUP_SAY_NAME")]
        public string CustomSayName { get; set; }

        [LocalizedDisplayName("SETUP_DISPLAY_SOCIAL")]
        [LinkedConfiguration("SocialLinkAddress", "SocialLinkTitle")]
        public bool EnableSocialLink { get; set; }
        [LocalizedDisplayName("SETUP_SOCIAL_LINK")]
        public string SocialLinkAddress { get; set; }
        [LocalizedDisplayName("SETUP_SOCIAL_TITLE")]
        public string SocialLinkTitle { get; set; }

        [LocalizedDisplayName("SETUP_USE_CUSTOMENCODING")]
        [LinkedConfiguration("CustomParserEncoding")]
        public bool EnableCustomParserEncoding { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENCODING")]
        public string CustomParserEncoding { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENABLE_WHITELIST")]
        [LinkedConfiguration("WebfrontConnectionWhitelist")]
        public bool EnableWebfrontConnectionWhitelist { get; set; }
        public List<string> WebfrontConnectionWhitelist { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_LOCALE")]
        [LinkedConfiguration("CustomLocale")]
        public bool EnableCustomLocale { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_LOCALE")]
        public string CustomLocale { get; set; }

        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DB_PROVIDER")]
        public string DatabaseProvider { get; set; } = "sqlite";
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CONNECTION_STRING")]
        public string ConnectionString { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_RCON_POLLRATE")]
        public int RConPollRate { get; set; } = 5000;
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MAX_TB")]
        public TimeSpan MaximumTempBanTime { get; set; } = new TimeSpan(24 * 30, 0, 0);

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGE_PERIOD")]
        public int AutoMessagePeriod { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGES")]
        public List<string> AutoMessages { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_GLOBAL_RULES")]
        public List<string> GlobalRules { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DISALLOWED_NAMES")]
        public List<string> DisallowedClientNames { get; set; }
        [UIHint("ServerConfiguration")]
        public List<ServerConfiguration> Servers { get; set; }


        [ConfigurationIgnore]
        public string Id { get; set; }
        [ConfigurationIgnore]
        public List<MapConfiguration> Maps { get; set; }
        [ConfigurationIgnore]
        public List<QuickMessageConfiguration> QuickMessages { get; set; }
        [ConfigurationIgnore]
        public string WebfrontUrl => string.IsNullOrEmpty(ManualWebfrontUrl) ? WebfrontBindUrl?.Replace("0.0.0.0", "127.0.0.1") : ManualWebfrontUrl;

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
            {
                CustomSayName = Utilities.PromptString(loc["SETUP_SAY_NAME"]);
            }

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

        public string Name()
        {
            return "ApplicationConfiguration";
        }
    }
}