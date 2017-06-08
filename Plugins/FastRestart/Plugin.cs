using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Network;

namespace Plugin
{
    public class FastRestartConfig : Serialize<FastRestartConfig>
    {
        public bool Enabled;
    }

    public class CEnableFastRestart : Command
    {
        public CEnableFastRestart() : base("frenable", "enable fast restarting at the end of a map. syntax: !fre", "fre", Player.Permission.SeniorAdmin, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            FastRestartPlugin.Config = new FastRestartConfig() { Enabled = true };
            Serialize<FastRestartConfig>.Write($"config/fastrestartconfig_{E.Owner}.cfg", FastRestartPlugin.Config);
            await E.Origin.Tell("Fast restarting is now enabled for this server");
        }
    }

    public class CDisableFastRestart : Command
    {
        public CDisableFastRestart() : base("fredisable", "disable fast restarting at the end of a map. syntax: !frd", "frd", Player.Permission.SeniorAdmin, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            FastRestartPlugin.Config = new FastRestartConfig() { Enabled = false };
            Serialize<FastRestartConfig>.Write($"config/fastrestartconfig_{E.Owner}.cfg", FastRestartPlugin.Config);
            await E.Origin.Tell("Fast restarting is now disabled for this server");
        }
    }

    public class FastRestartPlugin : IPlugin
    {
        bool MatchEnded;
        DateTime MatchEndTime;
        public static FastRestartConfig Config;

        public string Name { get { return "Fast Restarter"; } }

        public float Version { get { return 1.0f; } }

        public string Author { get { return "RaidMax"; } }

        public async Task OnEventAsync(Event E, Server S)
        {
           if (E.Type == Event.GType.Start)
            {
                try
                {
                    await S.GetDvarAsync<int>("scr_intermission_time");
                    Config = Serialize<FastRestartConfig>.Read($"config/fastrestartconfig_{E.Owner}.cfg");
                }

                catch (SharedLibrary.Exceptions.DvarException)
                {
                    await S.ExecuteCommandAsync("set scr_intermission_time 20");
                }

                catch (SharedLibrary.Exceptions.SerializeException)
                {
                    Config = new FastRestartConfig() { Enabled = false };
                    Serialize<FastRestartConfig>.Write($"config/fastrestartconfig_{E.Owner}.cfg", Config);
                }
            }
        }

        public Task OnLoadAsync()
        {
            return null;
        }

        public async Task OnTickAsync(Server S)
        {
            if (!Config.Enabled)
                return;

            MatchEnded = (await S.GetDvarAsync<int>("scr_gameended")).Value == 1;

            if (MatchEnded && MatchEndTime == DateTime.MinValue)
                MatchEndTime = DateTime.Now;

            // I'm pretty sure the timelength from game ended to scoreboard popup is 2000ms
            if (MatchEnded && (DateTime.Now - MatchEndTime).TotalSeconds >= ((await S.GetDvarAsync<int>("scr_intermission_time")).Value - 2))
            {
                await S.ExecuteCommandAsync("fast_restart");
                MatchEndTime = DateTime.MinValue;
            }
        }

        public Task OnUnloadAsync()
        {
            return null;
        }
    }
}
