using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats.Config
{
    class StatsConfiguration : IBaseConfiguration
    {
        public bool EnableAntiCheat { get; set; }
        public List<StreakMessageConfiguration> KillstreakMessages { get; set; }
        public List<StreakMessageConfiguration> DeathstreakMessages { get; set; }
        public string Name() => "Stats";
        public IBaseConfiguration Generate()
        {
            var config = new StatsConfiguration();

            Console.Write("Enable server-side anti-cheat? [y/n]: ");
            config.EnableAntiCheat = (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';

            config.KillstreakMessages = new List<StreakMessageConfiguration>()
            {
                new StreakMessageConfiguration(){
                    Count = -1,
                    Message = "Try not to kill yourself anymore"
                },
                 new StreakMessageConfiguration() {
                     Count = 5,
                     Message = "Great job! You're on a ^55 killstreak!"
                 },
                 new StreakMessageConfiguration()
                 {
                     Count = 10,
                     Message = "Amazing! ^510 kills ^7without dying!"
                 },
                 new StreakMessageConfiguration(){
                     Count = 25,
                     Message = "You better call in that nuke, ^525 killstreak^7!"
                 }
            };

            config.DeathstreakMessages = new List<StreakMessageConfiguration>()
            {
                 new StreakMessageConfiguration()
                 {
                     Count = 5,
                     Message = "Pick it up soldier, you've died ^55 times ^7in a row..."
                 },
                 new StreakMessageConfiguration(){
                     Count = 10,
                     Message = "Seriously? ^510 deaths ^7without getting a kill?"
                 },
            };

            return config;
        }
    }
}
