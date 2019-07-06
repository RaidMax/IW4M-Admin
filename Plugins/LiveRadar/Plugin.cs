using LiveRadar.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LiveRadar
{
    public class Plugin : IPlugin
    {
        public string Name => "Live Radar";

        public float Version => (float)Utilities.GetVersionAsDouble();

        public string Author => "RaidMax";

        internal static BaseConfigurationHandler<LiveRadarConfiguration> Config;

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (E.Type == GameEvent.EventType.Unknown)
            {
                if (E.Data?.StartsWith("LiveRadar") ?? false)
                {
                    var radarUpdate = RadarEvent.Parse(E.Data);
                    var client = S.Manager.GetActiveClients().FirstOrDefault(_client => _client.NetworkId == radarUpdate.Guid);

                    if (client != null)
                    {
                        radarUpdate.Name = client.Name;
                        client.SetAdditionalProperty("LiveRadar", radarUpdate);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            // load custom configuration
            Config = new BaseConfigurationHandler<LiveRadarConfiguration>("LiveRadarConfiguration");
            if (Config.Configuration() == null)
            {
                Config.Set((LiveRadarConfiguration)new LiveRadarConfiguration().Generate());
                await Config.Save();
            }
        }

        public Task OnTickAsync(Server S)
        {
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            return Task.CompletedTask;
        }
    }
}
