using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Network;
using SharedLibrary.Helpers;
using SharedLibrary.Objects;

namespace Plugin
{
    public class FastRestartConfig : Serialize<FastRestartConfig>
    {
        public bool Enabled;
    }

    public class CEnableFastRestart : Command
    {
        public CEnableFastRestart() : base("frenable", "enable fast restarting at the end of a map", "fre", Player.Permission.SeniorAdmin, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            FastRestartPlugin.ConfigManager.UpdateProperty(E.Owner, new KeyValuePair<string, object>("Enabled", true));
            await E.Origin.Tell("Fast restarting is now enabled for this server");
        }
    }

    public class CDisableFastRestart : Command
    {
        public CDisableFastRestart() : base("fredisable", "disable fast restarting at the end of a map", "frd", Player.Permission.SeniorAdmin, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            FastRestartPlugin.ConfigManager.UpdateProperty(E.Owner, new KeyValuePair<string, object>("Enabled", false));
            await E.Origin.Tell("Fast restarting is now disabled for this server");
        }
    }

    public class FastRestartPlugin : IPlugin
    {
        bool MatchEnded;
        DateTime MatchEndTime;
        Dictionary<int, int> FastRestarts;

        public static ConfigurationManager ConfigManager { get; private set; }

        public string Name => "Fast Restarter";

        public float Version => 1.0f;

        public string Author => "RaidMax";

        public async Task OnEventAsync(Event E, Server S)
        {
           if (E.Type == Event.GType.Start)
            {
                ConfigManager.AddConfiguration(S);
                if (ConfigManager.GetConfiguration(S).Keys.Count == 0)
                    ConfigManager.AddProperty(S, new KeyValuePair<string, object>("Enabled", false));

                FastRestarts.Add(S.GetHashCode(), 0);

                try
                {
                    await S.GetDvarAsync<int>("scr_intermission_time");
                }

                catch (SharedLibrary.Exceptions.DvarException)
                {
                    await S.SetDvarAsync("scr_intermission_time", 20);
                }
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
            ConfigManager = new ConfigurationManager(typeof(FastRestartPlugin));
            FastRestarts = new Dictionary<int, int>();
        }

        public async Task OnTickAsync(Server S)
        {
            if ((bool)ConfigManager.GetConfiguration(S)["Enabled"] == false)
                return;

            MatchEnded = (await S.GetDvarAsync<int>("scr_gameended")).Value == 1;

            if (MatchEnded && MatchEndTime == DateTime.MinValue)
                MatchEndTime = DateTime.Now;

            int intermissionTime = 20;

            try
            {
                var intermissionTimeDvar = await S.GetDvarAsync<int>("scr_intermission_time");
                intermissionTime = intermissionTimeDvar.Value;
            }
            
            catch(SharedLibrary.Exceptions.DvarException)
            {
                await S.SetDvarAsync("scr_intermission_time", 20);
            }

            // I'm pretty sure the timelength from game ended to scoreboard popup is 2000ms
            if (MatchEnded && (DateTime.Now - MatchEndTime).TotalSeconds >= intermissionTime - 2)
            {
                if (FastRestarts[S.GetHashCode()] >= 8)
                {
                    await S.ExecuteCommandAsync("map_restart");
                    FastRestarts[S.GetHashCode()] = 0;
                }

                else
                {
                    await S.ExecuteCommandAsync("fast_restart");
                    FastRestarts[S.GetHashCode()] = FastRestarts[S.GetHashCode()] + 1;
                }

                MatchEndTime = DateTime.MinValue;
            }
        }

        public async Task OnUnloadAsync()
        {
            
        }
    }
}
