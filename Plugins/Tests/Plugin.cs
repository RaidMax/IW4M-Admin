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

        private static DateTime Interval;

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

                     while (S.PlayerHistory.Count < 48)
                     {
                         S.PlayerHistory.Enqueue(new PlayerHistory(time, rand.Next(7, 18)));
                         time = time.AddMinutes(15);
                     }
                 });
                #endregion
            }
        }

        public async Task OnLoadAsync()
        {
            Interval = DateTime.Now;
        }

        public async Task OnTickAsync(Server S)
        {
            if ((DateTime.Now - Interval).TotalSeconds > 5)
            {
                var rand = new Random();
                int index = rand.Next(0, 17);
                var p = new Player($"Test_{index}", "_test", index, (int)Player.Permission.User)
                {
                    Ping = 1
                };

                p.SetIP("127.0.0.1");

                if (S.Players.ElementAt(index) != null)
                    await S.RemovePlayer(index);
                await S.AddPlayer(p);

                Interval = DateTime.Now;
            }
        }

        public async Task OnUnloadAsync()
        {

        }
    }
}
#endif