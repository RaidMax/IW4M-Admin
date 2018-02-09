using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Helpers
{
    public class StreakMessage
    {
        public static string MessageOnStreak(int killStreak, int deathStreak)
        {
            String Message = "";
            switch (killStreak)
            {
                case 5:
                    Message = "Great job! You're on a ^55 killstreak!";
                    break;
                case 10:
                    Message = "Amazing! ^510 kills ^7without dying!";
                    break;
            }

            switch (deathStreak)
            {
                case 5:
                    Message = "Pick it up soldier, you've died ^55 times ^7in a row...";
                    break;
                case 10:
                    Message = "Seriously? ^510 deaths ^7without getting a kill?";
                    break;
            }

            return Message;
        }
    }
}
