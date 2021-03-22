using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Config;
using System;
using System.Collections.Generic;
using static IW4MAdmin.Plugins.Stats.Cheat.Detection;

namespace IW4MAdmin.Plugins.Stats.Config
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
                Game = Server.Game.IW4,
                WeaponSuffix = "mp",
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
                Utilities.PromptBool(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_STATS_SETUP_ENABLEAC"]);
            KillstreakMessages = new List<StreakMessageConfiguration>()
            {
                new StreakMessageConfiguration()
                {
                    Count = -1,
                    Message = "Try not to kill yourself anymore"
                },
                new StreakMessageConfiguration()
                {
                    Count = 5,
                    Message = "Great job! You're on a ^55 killstreak!"
                },
                new StreakMessageConfiguration()
                {
                    Count = 10,
                    Message = "Amazing! ^510 kills ^7without dying!"
                },
                new StreakMessageConfiguration()
                {
                    Count = 25,
                    Message = "You better call in that nuke, ^525 killstreak^7!"
                }
            };

            DeathstreakMessages = new List<StreakMessageConfiguration>()
            {
                new StreakMessageConfiguration()
                {
                    Count = 5,
                    Message = "Pick it up soldier, you've died ^55 times ^7in a row..."
                },
                new StreakMessageConfiguration()
                {
                    Count = 10,
                    Message = "Seriously? ^510 deaths ^7without getting a kill?"
                },
            };

            TopPlayersMinPlayTime = 3600 * 3;
            StoreClientKills = false;

            return this;
        }
    }
}