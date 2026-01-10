using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Data.Models.Misc;
using SharedLibraryCore.Configuration.Attributes;
using SharedLibraryCore.Interfaces;
using static Data.Models.Client.EFClient;

namespace SharedLibraryCore.Configuration
{
    public class ApplicationConfiguration : IBaseConfiguration
    {
        [ConfigurationIgnore]
        public CommunityInformationConfiguration CommunityInformation { get; set; } =
            new();

        [Obsolete("Use Webfront.Enabled instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? EnableWebFront { get; set; }

        [Obsolete("Use Webfront.BindUrl instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WebfrontBindUrl { get; set; }

        [Obsolete("Use Webfront.ManualUrl instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ManualWebfrontUrl { get; set; }

        [Obsolete("Use Webfront.CustomBranding instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WebfrontCustomBranding { get; set; }

        [Obsolete("Use Webfront.PrimaryColor instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WebfrontPrimaryColor { get; set; }

        [Obsolete("Use Webfront.SecondaryColor instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WebfrontSecondaryColor { get; set; }

        [ConfigurationIgnore] public WebfrontConfiguration Webfront { get; set; } = new();

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

        [LocalizedDisplayName("SETUP_CONTACT_URI")]
        public string ContactUri { get; set; }

        [LocalizedDisplayName("SETUP_USE_CUSTOMENCODING")]
        [ConfigurationLinked("CustomParserEncoding")]
        public bool EnableCustomParserEncoding { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_ENCODING")]
        public string CustomParserEncoding { get; set; }

        [Obsolete("Use Webfront.EnableConnectionWhitelist instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? EnableWebfrontConnectionWhitelist { get; set; }

        [Obsolete("Use Webfront.ConnectionWhitelist instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? WebfrontConnectionWhitelist { get; set; }

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

        [ConfigurationIgnore] public string IngameAccentColorKey { get; set; } = "Cyan";

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGE_PERIOD")]
        public int AutoMessagePeriod { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_AUTOMESSAGES")]
        public string[] AutoMessages { get; set; } = Array.Empty<string>();

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_GLOBAL_RULES")]
        public string[] GlobalRules { get; set; } = Array.Empty<string>();

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_DISALLOWED_NAMES")]
        public string[] DisallowedClientNames { get; set; } = Array.Empty<string>();

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

        [Obsolete("Use Webfront.PermissionSets instead")]
        [ConfigurationIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>>? PermissionSets { get; set; }

        public Dictionary<string, Permission> MinimumAlertPermissions { get; set; } = new()
        {
            { nameof(EFInboxMessage), Permission.Trusted },
            { nameof(GameEvent.EventType.ConnectionLost), Permission.Administrator },
            { nameof(GameEvent.EventType.ConnectionRestored), Permission.Administrator }
        };

        [ConfigurationIgnore]
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_PRESET_BAN_REASONS")]
        public Dictionary<string, string> PresetPenaltyReasons { get; set; } = new()
            { { "afk", "Away from keyboard" }, { "ci", "Connection interrupted. Reconnect" } };
        
        [ConfigurationIgnore] public bool EnableImplicitAccountLinking { get; set; } = false;
        [ConfigurationIgnore] public TimeSpan RecentAliasIpLinkTimeLimit { get; set; } = TimeSpan.FromDays(7);

        [ConfigurationIgnore] public TimeSpan MaxClientHistoryTime { get; set; } = TimeSpan.FromHours(12);

        [ConfigurationIgnore] public TimeSpan ServerDataCollectionInterval { get; set; } = TimeSpan.FromMinutes(5);

        [ConfigurationIgnore] public TimeSpan FailStateDetectionThreshold { get; set; } = TimeSpan.FromHours(2);

        [ConfigurationIgnore] public int FailStateMinPlayers { get; set; } = 1;

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

        [ConfigurationIgnore]
        [JsonIgnore]
        public string WebfrontUrl => string.IsNullOrEmpty(Webfront.ManualUrl)
            ? Webfront.BindUrl?.Replace("0.0.0.0", "127.0.0.1")
            : Webfront.ManualUrl;

        [ConfigurationIgnore] public bool IgnoreServerConnectionLost { get; set; }
        [ConfigurationIgnore] public Uri MasterUrl { get; set; } = new("https://master.iw4.zip");

        public IBaseConfiguration Generate()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            Id = Guid.NewGuid().ToString();

            Webfront.Enabled = loc["SETUP_ENABLE_WEBFRONT"].PromptBool();
            EnableMultipleOwners = loc["SETUP_ENABLE_MULTIOWN"].PromptBool();
            Webfront.BindUrl = "http://0.0.0.0:1624";

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
