#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Helpers;

namespace IW4MAdmin.Plugins
{
    public class Tests : IPlugin
    {
        public string Name => "Dev Tests";

        public float Version => 0.1f;

        public string Author => "RaidMax";

        private DateTime Interval;

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                #region PLAYER_HISTORY
                var rand = new Random(GetHashCode());
                var time = DateTime.UtcNow;

                await Task.Run(() =>
                 {
                     if (S.PlayerHistory.Count > 0)
                         return;

                     while (S.PlayerHistory.Count < 144)
                     {
                         S.PlayerHistory.Enqueue(new PlayerHistory(time, rand.Next(7, 18)));
                         time = time.AddMinutes(PlayerHistory.UpdateInterval);
                     }
                 });
                #endregion

                #region PLUGIN_INFO
                foreach (var command in S.Manager.GetCommands().OrderByDescending(c => c.Permission).ThenBy(c => c.Name))
                {
                    //|Name|Description|Requires Target|Arg Count|Required Level|
                   Console.WriteLine($"|{command.Name}|{command.Alias}|{command.Description.Split('.')[0]}|{command.RequiresTarget}|{command.RequiredArgumentCount}|{command.Permission}|");
                }
                #endregion
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
            Interval = DateTime.Now;
        }

        public async Task OnTickAsync(Server S)
        {
            if ((DateTime.Now - Interval).TotalSeconds > 5)
            {
                var rand = new Random();
                int index = rand.Next(0, 17);
                var p = new Player($"Test_{index}", $"_test{index}", index, (int)Player.Permission.User)
                {
                    Ping = 1
                };

                p.SetIP("127.0.0.1");

                if (S.Players.ElementAt(index) != null)
                    await S.RemovePlayer(index);
                await S.AddPlayer(p);


                Interval = DateTime.Now;
                if (S.ClientNum > 0)
                {

                    //"K;26d2f66b95184934;1;allies;egor;5c56fef676b3818d;0;axis;1_din;m21_heartbeat_mp;98;MOD_RIFLE_BULLET;torso_lower";
                    var victimPlayer = S.Players.Where(pl => pl != null).ToList()[rand.Next(0, S.ClientNum - 1)];
                    var attackerPlayer = S.Players.Where(pl => pl != null).ToList()[rand.Next(0, S.ClientNum - 1)];

                    await S.ExecuteEvent(new Event(Event.GType.Say, $"test_{attackerPlayer.ClientID}", victimPlayer, attackerPlayer, S));

                    string[] eventLine = null;

                    for (int i = 0; i < 1; i++)
                    {
                        if (S.GameName == Server.Game.IW4)
                        {

                            // attackerID ; victimID ; attackerOrigin ; victimOrigin ; Damage ; Weapon ; hitLocation ; meansOfDeath
                            var minimapInfo = StatsPlugin.MinimapConfig.IW4Minimaps().MapInfo.FirstOrDefault(m => m.MapName == S.CurrentMap.Name);
                            if (minimapInfo == null)
                                return;
                            eventLine = new string[]
                            {
                            "ScriptKill",
                            attackerPlayer.NetworkID,
                            victimPlayer.NetworkID,
                            new Vector3(rand.Next(minimapInfo.MaxRight, minimapInfo.MaxLeft), rand.Next(minimapInfo.MaxBottom, minimapInfo.MaxTop), rand.Next(0, 100)).ToString(),
                            new Vector3(rand.Next(minimapInfo.MaxRight, minimapInfo.MaxLeft), rand.Next(minimapInfo.MaxBottom, minimapInfo.MaxTop), rand.Next(0, 100)).ToString(),
                            rand.Next(50, 105).ToString(),
                            ((StatsPlugin.IW4Info.WeaponName)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.WeaponName)).Length - 1)).ToString(),
                            ((StatsPlugin.IW4Info.HitLocation)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.HitLocation)).Length - 1)).ToString(),
                            ((StatsPlugin.IW4Info.MeansOfDeath)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.MeansOfDeath)).Length - 1)).ToString()
                            };

                        }
                        else
                        {
                            eventLine = new string[]
                           {
                            "K",
                            victimPlayer.NetworkID,
                            victimPlayer.ClientID.ToString(),
                            rand.Next(0, 1) == 0  ? "allies" : "axis",
                            victimPlayer.Name,
                            attackerPlayer.NetworkID,
                            attackerPlayer.ClientID.ToString(),
                            rand.Next(0, 1) == 0  ? "allies" : "axis",
                            attackerPlayer.Name.ToString(),
                            ((StatsPlugin.IW4Info.WeaponName)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.WeaponName)).Length - 1)).ToString(), // Weapon
                            rand.Next(50, 105).ToString(),                                  // Damage
                            ((StatsPlugin.IW4Info.MeansOfDeath)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.MeansOfDeath)).Length - 1)).ToString(),  // Means of Death
                            ((StatsPlugin.IW4Info.HitLocation)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.HitLocation)).Length - 1)).ToString(),                 // Hit Location
                           };
                        }

                        var _event = Event.ParseEventString(eventLine, S);
                        await S.ExecuteEvent(_event);
                    }
                }
            }

        }

        public async Task OnUnloadAsync()
        {

        }
    }
}
#endif