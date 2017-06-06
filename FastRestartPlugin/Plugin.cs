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
    public class FastRestartPlugin : IPlugin
    {
        bool MatchEnded;
        DateTime MatchEndTime;

        public string Name { get { return "Fast Restart"; } }

        public float Version { get { return 1.0f; } }

        public string Author { get { return "RaidMax"; } }

        public async Task OnEventAsync(Event E, Server S)
        {
           if (E.Type == Event.GType.Start)
            {
                try
                {
                    await S.GetDvarAsync<int>("scr_intermission_time");
                }

                catch (SharedLibrary.Exceptions.DvarException)
                {
                    await S.ExecuteCommandAsync("set scr_intermission_time 20");
                }
            }
        }

        public async Task OnLoadAsync()
        {
        }

        public async Task OnTickAsync(Server S)
        {
            MatchEnded = (await S.GetDvarAsync<int>("scr_gameended")).Value == 1;

            if (MatchEnded && MatchEndTime == DateTime.MinValue)
                MatchEndTime = DateTime.Now;

            if (MatchEnded && (DateTime.Now - MatchEndTime).TotalSeconds > (await S.GetDvarAsync<int>("scr_intermission_time")).Value - 5)
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
