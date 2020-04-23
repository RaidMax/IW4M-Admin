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

        private readonly IConfigurationHandler<LiveRadarConfiguration> _configurationHandler;
        private bool addedPage;
        private object lockObject;

        public Plugin(IConfigurationHandlerFactory configurationHandlerFactory)
        {
            _configurationHandler = configurationHandlerFactory.GetConfigurationHandler<LiveRadarConfiguration>("LiveRadarConfiguration");
        }

        public Task OnEventAsync(GameEvent E, Server S)
        {
            // if it's an IW4 game, with custom callbacks, we want to 
            // enable the live radar page
            lock (lockObject)
            {
                if (E.Type == GameEvent.EventType.Start &&
                    S.GameName == Server.Game.IW4 &&
                    S.CustomCallback &&
                    !addedPage)
                {
                    E.Owner.Manager.GetPageList().Pages.Add(Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"], "/Radar/All");
                    addedPage = true;
                }
            }

            if (E.Type == GameEvent.EventType.Unknown)
            {
                if (E.Data?.StartsWith("LiveRadar") ?? false)
                {
                    try
                    {
                        var radarUpdate = RadarEvent.Parse(E.Data);
                        var client = S.Manager.GetActiveClients().FirstOrDefault(_client => _client.NetworkId == radarUpdate.Guid);

                        if (client != null)
                        {
                            radarUpdate.Name = client.Name.StripColors();
                            client.SetAdditionalProperty("LiveRadar", radarUpdate);
                        }
                    }

                    catch (Exception e)
                    {
                        S.Logger.WriteWarning($"Could not parse live radar output: {e.Data}");
                        S.Logger.WriteDebug(e.GetExceptionInfo());
                    }
                }
            }

            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            if (_configurationHandler.Configuration() == null)
            {
                _configurationHandler.Set((LiveRadarConfiguration)new LiveRadarConfiguration().Generate());
                await _configurationHandler.Save();
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
