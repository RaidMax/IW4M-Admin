using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using SharedLibraryCore.Configuration.Attributes;
using SharedLibraryCore.Interfaces;
using static Data.Models.Client.EFClient;

namespace SharedLibraryCore.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {
        [ConfigurationIgnore]
        public CommunityInformationConfiguration CommunityInformation { get; set; } =
            new CommunityInformationConfiguration();

        [LocalizedDisplayName("SETUP_ENABLE_WEBFRONT")]
        [ConfigurationLinked("WebfrontBindUrl", "ManualWebfrontUrl", "WebfrontPrimaryColor", "WebfrontSecondaryColor",
            "WebfrontCustomBranding")]
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

        [ConfigurationIgnore] public WebfrontConfiguration Webfront { get; set; } = new WebfrontConfiguration();

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

        [LocalizedDisplayName("SETUP_CONTACT_URI")]
        public string ContactUri { get; set; }

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
        public int RConPollRate { get; set; } = 8000;

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MAX_TB")]
        public TimeSpan MaximumTempBanTime { get; set; } = new TimeSpan(24 * 30, 0, 0);

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENABLE_COLOR_CODES")]
        public bool EnableColorCodes { get; set; }

        [ConfigurationIgnore] public string IngameAccentColorKey { get; set; } = "Cyan";

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGE_PERIOD")]
        public int AutoMessagePeriod { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGES")]
        public string[] AutoMessages { get; set; } = new string[0];

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_GLOBAL_RULES")]
        public string[] GlobalRules { get; set; } = new string[0];

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DISALLOWED_NAMES")]
        public string[] DisallowedClientNames { get; set; } = new string[0];

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_MAP_CHANGE_DELAY")]
        public int MapChangeDelaySeconds { get; set; } = 5;

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_BAN_DURATIONS")]
        public TimeSpan[] BanDurations { get; set; } =
        {
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(2),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30)
        };

        [ConfigurationIgnore]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_PRESET_BAN_REASONS")]
        public Dictionary<string, string> PresetPenaltyReasons { get; set; } = new Dictionary<string, string>
            { { "afk", "Away from keyboard" }, { "ci", "Connection interrupted. Reconnect" } };

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENABLE_PRIVILEGED_USER_PRIVACY")]
        public bool EnablePrivilegedUserPrivacy { get; set; }

        [ConfigurationIgnore] public bool EnableImplicitAccountLinking { get; set; } = false;
        [ConfigurationIgnore] public TimeSpan RecentAliasIpLinkTimeLimit { get; set; } = TimeSpan.FromDays(7);

        [ConfigurationIgnore] public TimeSpan MaxClientHistoryTime { get; set; } = TimeSpan.FromHours(12);

        [ConfigurationIgnore] public TimeSpan ServerDataCollectionInterval { get; set; } = TimeSpan.FromMinutes(5);

        public int ServerConnectionAttempts { get; set; } = 6;

        [ConfigurationIgnore]
        public Dictionary<Permission, string> OverridePermissionLevelNames { get; set; } = Enum
            .GetValues(typeof(Permission))
            .Cast<Permission>()
            .ToDictionary(perm => perm, perm => perm.ToString());

        [UIHint("ServerConfiguration")] public ServerConfiguration[] Servers { get; set; }

        [ConfigurationIgnore] public int MinimumNameLength { get; set; } = 3;
        [ConfigurationIgnore] public string Id { get; set; }
        [ConfigurationIgnore] public string SubscriptionId { get; set; }

        [Obsolete("Moved to DefaultSettings")]
        [ConfigurationIgnore]
        public MapConfiguration[] Maps { get; set; }

        [Obsolete("Moved to DefaultSettings")]
        [ConfigurationIgnore]
        public QuickMessageConfiguration[] QuickMessages { get; set; }

        [ConfigurationIgnore]
        [JsonIgnore]
        public string WebfrontUrl => string.IsNullOrEmpty(ManualWebfrontUrl)
            ? WebfrontBindUrl?.Replace("0.0.0.0", "127.0.0.1")
            : ManualWebfrontUrl;

        [ConfigurationIgnore] public bool IgnoreServerConnectionLost { get; set; }
        [ConfigurationIgnore] public Uri MasterUrl { get; set; } = new Uri("http://api.raidmax.org:5000");

        public IBaseConfiguration Generate()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            Id = Guid.NewGuid().ToString();

            EnableWebFront = loc["SETUP_ENABLE_WEBFRONT"].PromptBool();
            EnableMultipleOwners = loc["SETUP_ENABLE_MULTIOWN"].PromptBool();
            EnableSteppedHierarchy = loc["SETUP_ENABLE_STEPPEDPRIV"].PromptBool();
            EnableCustomSayName = loc["SETUP_ENABLE_CUSTOMSAY"].PromptBool();

            var useCustomParserEncoding = loc["SETUP_USE_CUSTOMENCODING"].PromptBool();
            if (useCustomParserEncoding)
            {
                CustomParserEncoding = loc["SETUP_ENCODING_STRING"].PromptString();
            }

            WebfrontBindUrl = "http://0.0.0.0:1624";

            if (EnableCustomSayName)
            {
                CustomSayName = loc["SETUP_SAY_NAME"].PromptString();
            }

            EnableSocialLink = loc["SETUP_DISPLAY_SOCIAL"].PromptBool();

            if (EnableSocialLink)
            {
                SocialLinkTitle = loc["SETUP_SOCIAL_TITLE"].PromptString();
                SocialLinkAddress = loc["SETUP_SOCIAL_LINK"].PromptString();
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
