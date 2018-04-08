using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    public class StreakMessage
    {
        /// <summary>
        /// Get a message from the configuration encouraging or discouraging clients
        /// </summary>
        /// <param name="killStreak">how many kills the client has without dying</param>
        /// <param name="deathStreak">how many deaths the client has without getting a kill</param>
        /// <returns>message to send to the client</returns>
        public static string MessageOnStreak(int killStreak, int deathStreak)
        {
            var killstreakMessage = Plugin.Config.Configuration().KillstreakMessages;
            var deathstreakMessage = Plugin.Config.Configuration().DeathstreakMessages;

            string message = killstreakMessage.FirstOrDefault(m => m.Count == killStreak)?.Message;
            message = message ?? deathstreakMessage.FirstOrDefault(m => m.Count == deathStreak)?.Message;
            return message ?? "";
        }
    }
}
