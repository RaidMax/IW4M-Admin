using System;
using System.Collections.Generic;
using IW4MAdmin.Plugins.Stats.Config;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using static IW4MAdmin.Plugins.Stats.Cheat.Detection;

namespace Stats.Config
{
    public class StatsConfiguration : IBaseConfiguration
    {
        [Obsolete] public bool? EnableAntiCheat { get; set; }
        public List<StreakMessageConfiguration> KillstreakMessages { get; set; }
        public List<StreakMessageConfiguration> DeathstreakMessages { get; set; }
        public int TopPlayersMinPlayTime { get; set; }
        public bool StoreClientKills { get; set; }
        public int MostKillsMaxInactivityDays { get; set; } = 30;
        public int MostKillsClientLimit { get; set; } = 5;
        public bool EnableAdvancedMetrics { get; set; } = true;

        public WeaponNameParserConfiguration[] WeaponNameParserConfigurations { get; set; } = new[]
        {
            new WeaponNameParserConfiguration()
            {
                Game = Server.Game.IW3,
                WeaponSuffix = "mp",
                Delimiters = new[] {'_'}
            },
            new WeaponNameParserConfiguration()
            {
                Game = Server.Game.IW4,
                WeaponSuffix = "mp",
                Delimiters = new[] {'_'}
            },
            new WeaponNameParserConfiguration()
            {
                Game = Server.Game.IW5,
                WeaponSuffix = "mp",
                WeaponPrefix = "iw5",
                Delimiters = new[] {'_'}
            },
            new WeaponNameParserConfiguration()
            {
                Game = Server.Game.T6,
                WeaponSuffix = "mp",
                Delimiters = new[] {'_', '+'}
            }
        };

        [Obsolete] public IDictionary<long, DetectionType[]> ServerDetectionTypes { get; set; }
        public AnticheatConfiguration AnticheatConfiguration { get; set; } = new AnticheatConfiguration();

#pragma warning disable CS0612 // Type or member is obsolete
        public void ApplyMigration()
        {
            if (ServerDetectionTypes != null)
            {
                AnticheatConfiguration.ServerDetectionTypes = ServerDetectionTypes;
            }

            ServerDetectionTypes = null;

            if (EnableAntiCheat != null)
            {
                AnticheatConfiguration.Enable = EnableAntiCheat.Value;
            }

            EnableAntiCheat = null;
        }
#pragma warning restore CS0612 // Type or member is obsolete

        public string Name() => "StatsPluginSettings";

        public IBaseConfiguration Generate()
        {
            AnticheatConfiguration.Enable =
                Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_STATS_SETUP_ENABLEAC"].PromptBool();
            KillstreakMessages = new List<StreakMessageConfiguration>
            {
                new StreakMessageConfiguration
                {
                    Count = -1,
                    Message = Utilities.CurrentLocalization.LocalizationIndex["STATS_STREAK_MESSAGE_SUICIDE"]
                },
                new StreakMessageConfiguration
                {
                    Count = 5,
                    Message = Utilities.CurrentLocalization.LocalizationIndex["STATS_STREAK_MESSAGE_5"]
                },
                new StreakMessageConfiguration
                {
                    Count = 10,
                    Message = Utilities.CurrentLocalization.LocalizationIndex["STATS_STREAK_MESSAGE_10"]
                },
                new StreakMessageConfiguration
                {
                    Count = 25,
                    Message = Utilities.CurrentLocalization.LocalizationIndex["STATS_STREAK_MESSAGE_25"]
                }
            };

            DeathstreakMessages = new List<StreakMessageConfiguration>()
            {
                new StreakMessageConfiguration()
                {
                    Count = 5,
                    Message = Utilities.CurrentLocalization.LocalizationIndex["STATS_DEATH_STREAK_MESSAGE_5"]
                },
                new StreakMessageConfiguration()
                {
                    Count = 10,
                    Message = Utilities.CurrentLocalization.LocalizationIndex["STATS_DEATH_STREAK_MESSAGE_10"]
                },
            };

            TopPlayersMinPlayTime = 3600 * 3;
            StoreClientKills = false;

            return this;
        }
    }
}