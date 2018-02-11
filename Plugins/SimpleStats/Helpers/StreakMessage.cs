using SharedLibrary;
using SharedLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Helpers
{
    public class StreakMessage
    {
        private ConfigurationManager config;

        public StreakMessage(Server sv)
        {
            config = new ConfigurationManager(sv);

            // initialize default messages
            if (config.GetProperty<Dictionary<int, string>>("KillstreakMessages") == null)
            {
                var killstreakMessages = new Dictionary<int, string>()
                {
                    { -1,  "Try not to kill  yourself anymore" },
                    { 5,  "Great job! You're on a ^55 killstreak!" },
                    { 10,  "Amazing! ^510 kills ^7without dying!" },
                    { 25, "You better call in that nuke, ^525 killstreak!" }
                };
                config.AddProperty(new KeyValuePair<string, object>("KillstreakMessages", killstreakMessages));
            }

            if (config.GetProperty<Dictionary<int, string>>("DeathstreakMessages") == null)
            {
                var deathstreakMessages = new Dictionary<int, string>()
                {
                    { 5,  "Pick it up soldier, you've died ^55 times ^7in a row..." },
                    { 10,  "Seriously? ^510 deaths ^7without getting a kill?" },
                };
                config.AddProperty(new KeyValuePair<string, object>("DeathstreakMessages", deathstreakMessages));
            }
        }

        /// <summary>
        /// Get a message from the configuration encouraging or discouraging clients
        /// </summary>
        /// <param name="killStreak">how many kills the client has without dying</param>
        /// <param name="deathStreak">how many deaths the client has without getting a kill</param>
        /// <returns>message to send to the client</returns>
        public string MessageOnStreak(int killStreak, int deathStreak)
        {
            var killstreakMessage = config.GetProperty<Dictionary<int, string>>("KillstreakMessages");
            var deathstreakMessage = config.GetProperty<Dictionary<int, string>>("DeathstreakMessages");

            string message = "";

            if (killstreakMessage.ContainsKey(killStreak))
                message =killstreakMessage[killStreak];
            else if (deathstreakMessage.ContainsKey(deathStreak))
                message = deathstreakMessage[deathStreak];

            return message;
        }
    }
}
