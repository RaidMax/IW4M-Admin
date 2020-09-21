using LiveRadar.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, long> _botGuidLookups;
        private bool addedPage;
        private readonly object lockObject = new object();

        public Plugin(IConfigurationHandlerFactory configurationHandlerFactory)
        {
            _configurationHandler = configurationHandlerFactory.GetConfigurationHandler<LiveRadarConfiguration>("LiveRadarConfiguration");
            _botGuidLookups = new Dictionary<string, long>();
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

            if (E.Type == GameEvent.EventType.PreConnect && E.Origin.IsBot)
            {
                string botKey = $"BotGuid_{E.Extra}";
                lock (lockObject)
                {
                    if (!_botGuidLookups.ContainsKey(botKey))
                    {
                        _botGuidLookups.Add(botKey, E.Origin.NetworkId);
                    }
                }
            }

            if (E.Type == GameEvent.EventType.Other && E.Subtype == "LiveRadar")
            {
                try
                {
                    string botKey = $"BotGuid_{E.Extra}";
                    long generatedBotGuid;

                    lock (lockObject)
                    {
                        generatedBotGuid = _botGuidLookups.ContainsKey(botKey) ? _botGuidLookups[botKey] : 0;
                    }

                    var radarUpdate = RadarEvent.Parse(E.Data, generatedBotGuid);
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
