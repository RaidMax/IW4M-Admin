using LiveRadar.Configuration;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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
        private readonly ILogger _logger;
        private readonly ApplicationConfiguration _appConfig;

        public Plugin(ILogger<Plugin> logger, IConfigurationHandlerFactory configurationHandlerFactory, ApplicationConfiguration appConfig)
        {
            _configurationHandler = configurationHandlerFactory.GetConfigurationHandler<LiveRadarConfiguration>("LiveRadarConfiguration");
            _botGuidLookups = new Dictionary<string, long>();
            _logger = logger;
            _appConfig = appConfig;
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
                    E.Owner.Manager.GetPageList().Pages.Add(Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"], "/Radar");
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
                    if (((string) E.Extra).IsBotGuid() && _appConfig.IgnoreBots)
                    {
                        return Task.CompletedTask;
                    }
                    
                    string botKey = $"BotGuid_{E.Extra}";
                    long generatedBotGuid;

                    lock (lockObject)
                    {
                        var hasBotKey = _botGuidLookups.ContainsKey(botKey);

                        if (!hasBotKey && ((string)E.Extra).IsBotGuid())
                        {
                            // edge case where the bot guid has not been registered yet
                            return Task.CompletedTask;
                        }
                        
                        generatedBotGuid = hasBotKey
                            ? _botGuidLookups[botKey] 
                            : (E.Extra.ToString() ?? "0").ConvertGuidToLong(NumberStyles.HexNumber);
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
                    _logger.LogError(e, "Could not parse live radar output: {data}", e.Data);
                }
            }

            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            await _configurationHandler.BuildAsync();
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
