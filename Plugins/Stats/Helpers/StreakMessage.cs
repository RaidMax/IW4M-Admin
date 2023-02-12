using System.Linq;
using Stats.Config;

namespace IW4MAdmin.Plugins.Stats.Helpers;

public static class StreakMessage
{
    public static string MessageOnStreak(int killStreak, int deathStreak, StatsConfiguration config)
    {
        var killstreakMessage = config.KillstreakMessages;
        var deathstreakMessage = config.DeathstreakMessages;

        var message = killstreakMessage?.FirstOrDefault(m => m.Count == killStreak)?.Message;
        message ??= deathstreakMessage?.FirstOrDefault(m => m.Count == deathStreak)?.Message;
        return message ?? "";
    }
}
