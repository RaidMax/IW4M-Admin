using SharedLibraryCore.Configuration.Attributes;
using SharedLibraryCore.Interfaces;
using System;
using System.ComponentModel.DataAnnotations;

namespace SharedLibraryCore.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {
        [LocalizedDisplayName("SETUP_ENABLE_WEBFRONT")]
        [ConfigurationLinked("WebfrontBindUrl", "ManualWebfrontUrl", "WebfrontPrimaryColor", "WebfrontSecondaryColor", "WebfrontCustomBranding")]
        public bool EnableWebFront { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_BIND_URL")]
        public string WebfrontBindUrl { get; set; }
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MANUAL_URL")]
        public string ManualWebfrontUrl { get; set; }
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_PRIMARY_COLOR")]
        public string WebfrontPrimaryColor { get; set; }
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SECONDARY_COLOR")]
        public string WebfrontSecondaryColor { get; set; }
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_BRANDING")]
        public string WebfrontCustomBranding { get; set; }

        [LocalizedDisplayName("SETUP_ENABLE_MULTIOWN")]
        public bool EnableMultipleOwners { get; set; }
        [LocalizedDisplayName("SETUP_ENABLE_STEPPEDPRIV")]
        public bool EnableSteppedHierarchy { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_USE_LOCAL_TRANSLATIONS")]
        public bool UseLocalTranslations { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_IGNORE_BOTS")]
        public bool IgnoreBots { get; set; }

        [ConfigurationLinked("CustomSayName")]
        [LocalizedDisplayName("SETUP_ENABLE_CUSTOMSAY")]
        public bool EnableCustomSayName { get; set; }
        [LocalizedDisplayName("SETUP_SAY_NAME")]
        public string CustomSayName { get; set; }

        [LocalizedDisplayName("SETUP_DISPLAY_SOCIAL")]
        [ConfigurationLinked("SocialLinkAddress", "SocialLinkTitle")]
        public bool EnableSocialLink { get; set; }
        [LocalizedDisplayName("SETUP_SOCIAL_LINK")]
        public string SocialLinkAddress { get; set; }
        [LocalizedDisplayName("SETUP_SOCIAL_TITLE")]
        public string SocialLinkTitle { get; set; }

        [LocalizedDisplayName("SETUP_USE_CUSTOMENCODING")]
        [ConfigurationLinked("CustomParserEncoding")]
        public bool EnableCustomParserEncoding { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENCODING")]
        public string CustomParserEncoding { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENABLE_WHITELIST")]
        [ConfigurationLinked("WebfrontConnectionWhitelist")]
        public bool EnableWebfrontConnectionWhitelist { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_WHITELIST_LIST")]
        public string[] WebfrontConnectionWhitelist { get; set; } = new string[0];

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_LOCALE")]
        [ConfigurationLinked("CustomLocale")]
        public bool EnableCustomLocale { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CUSTOM_LOCALE")]
        public string CustomLocale { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_COMMAND_PREFIX")]
        public string CommandPrefix { get; set; } = "!";

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_BROADCAST_COMMAND_PREFIX")]
        public string BroadcastCommandPrefix { get; set; } = "@";

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DB_PROVIDER")]
        public string DatabaseProvider { get; set; } = "sqlite";
        [ConfigurationOptional]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_CONNECTION_STRING")]
        public string ConnectionString { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_RCON_POLLRATE")]
        public int RConPollRate { get; set; } = 5000;
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MAX_TB")]
        public TimeSpan MaximumTempBanTime { get; set; } = new TimeSpan(24 * 30, 0, 0);
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENABLE_COLOR_CODES")]
        public bool EnableColorCodes { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGE_PERIOD")]
        public int AutoMessagePeriod { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGES")]
        public string[] AutoMessages { get; set; } = new string[0];
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_GLOBAL_RULES")]
        public string[] GlobalRules { get; set; } = new string[0];
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DISALLOWED_NAMES")]
        public string[] DisallowedClientNames { get; set; } = new string[0];
        [UIHint("ServerConfiguration")]
        public ServerConfiguration[] Servers { get; set; }

        [ConfigurationIgnore]
        public string Id { get; set; }
        [ConfigurationIgnore]
        public MapConfiguration[] Maps { get; set; }
        [ConfigurationIgnore]
        public QuickMessageConfiguration[] QuickMessages { get; set; }
        [ConfigurationIgnore]
        public string WebfrontUrl => string.IsNullOrEmpty(ManualWebfrontUrl) ? WebfrontBindUrl?.Replace("0.0.0.0", "127.0.0.1") : ManualWebfrontUrl;
        [ConfigurationIgnore]
        public bool IgnoreServerConnectionLost { get; set; }
        [ConfigurationIgnore]
        public Uri MasterUrl { get; set; } = new Uri("http://api.raidmax.org:5000");

        public IBaseConfiguration Generate()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            Id = Guid.NewGuid().ToString();

            EnableWebFront = Utilities.PromptBool(loc["SETUP_ENABLE_WEBFRONT"]);
            EnableMultipleOwners = Utilities.PromptBool(loc["SETUP_ENABLE_MULTIOWN"]);
            EnableSteppedHierarchy = Utilities.PromptBool(loc["SETUP_ENABLE_STEPPEDPRIV"]);
            EnableCustomSayName = Utilities.PromptBool(loc["SETUP_ENABLE_CUSTOMSAY"]);

            bool useCustomParserEncoding = Utilities.PromptBool(loc["SETUP_USE_CUSTOMENCODING"]);
            if (useCustomParserEncoding)
            {
                CustomParserEncoding = Utilities.PromptString(loc["SETUP_ENCODING_STRING"]);
            }

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
            AutoMessagePeriod = 60;
            return this;
        }

        public string Name()
        {
            return "ApplicationConfiguration";
        }
    }
}