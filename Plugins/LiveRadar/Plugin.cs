using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using IW4MAdmin.Plugins.LiveRadar.Configuration;
using IW4MAdmin.Plugins.LiveRadar.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Plugins.LiveRadar;

public class Plugin : IPluginV2
{
    public string Name => "Live Radar";

    public string Version => Utilities.GetVersionAsString();

    public string Author => "RaidMax";

    private bool _addedPage;
    private readonly Dictionary<string, long> _botGuidLookups;
    private readonly object _lockObject = new();
    private readonly ILogger _logger;
    private readonly ApplicationConfiguration _appConfig;

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<LiveRadarConfiguration>();
        serviceCollection.AddHttpClient("LiveRadar7DTD", client =>
            client.Timeout = TimeSpan.FromSeconds(6))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            })
            .RedactLoggedHeaders(["X-SDTD-API-TOKENNAME", "X-SDTD-API-SECRET"]);

        serviceCollection.AddSingleton<IGameScriptEvent, LiveRadarScriptEvent>(); // for identification 
        serviceCollection.AddTransient<LiveRadarScriptEvent>(); // for factory
    }

    public Plugin(ILogger<Plugin> logger, ApplicationConfiguration appConfig)
    {
        _botGuidLookups = new Dictionary<string, long>();
        _logger = logger;
        _appConfig = appConfig;
        
        IGameServerEventSubscriptions.MonitoringStarted += OnMonitoringStarted;
        IGameEventSubscriptions.ClientEnteredMatch += OnClientEnteredMatch;
        IGameEventSubscriptions.ScriptEventTriggered += OnScriptEvent;
    }

    private Task OnScriptEvent(GameScriptEvent scriptEvent, CancellationToken token)
    {
        if (scriptEvent is not LiveRadarScriptEvent radarEvent)
        {
            return Task.CompletedTask;
        }
        
        try
        {
            var originalBotGuid = radarEvent.ScriptData.Split(";")[1];
            
            if (originalBotGuid.IsBotGuid() && _appConfig.IgnoreBots)
            {
                return Task.CompletedTask;
            }
                    
            var botKey = $"BotGuid_{originalBotGuid}";
            long generatedBotGuid;

            lock (_lockObject)
            {
                var hasBotKey = _botGuidLookups.ContainsKey(botKey);

                if (!hasBotKey && originalBotGuid.IsBotGuid())
                {
                    // edge case where the bot guid has not been registered yet
                    return Task.CompletedTask;
                }

                generatedBotGuid = hasBotKey
                    ? _botGuidLookups[botKey]
                    : (originalBotGuid ?? "0").ConvertGuidToLong(NumberStyles.HexNumber);
            }

            var radarDto = RadarDto.FromScriptEvent(radarEvent, generatedBotGuid);

            var client =
                radarEvent.Owner.ConnectedClients.FirstOrDefault(client => client.NetworkId == radarDto.Guid);

            if (client != null)
            {
                radarDto.Name = client.Name.StripColors();
                client.SetAdditionalProperty("LiveRadar", radarDto);
            }
        }

        catch (Exception e)
        {
            _logger.LogError(e, "Could not parse live radar output: {Data}", e.Data);
        }
        
        return Task.CompletedTask;
    }

    private Task OnClientEnteredMatch(ClientEnterMatchEvent clientEvent, CancellationToken token)
    {
        if (!clientEvent.Client.IsBot)
        {
            return Task.CompletedTask;
        }

        var botKey = $"BotGuid_{clientEvent.ClientNetworkId}";
        lock (_lockObject)
        {
            if (!_botGuidLookups.ContainsKey(botKey))
            {
                _botGuidLookups.Add(botKey, clientEvent.Client.NetworkId);
            }
        }

        return Task.CompletedTask;
    }

    private Task OnMonitoringStarted(MonitorStartEvent monitorEvent, CancellationToken token)
    {
        lock (_lockObject)
        {
            // if it's an IW4 game, with custom callbacks, we want to 
            // enable the live radar page
            var supportsIw4 = monitorEvent.Server.GameCode == Reference.Game.IW4 &&
                              monitorEvent.Server.IsLegacyGameIntegrationEnabled;
            var supportsSevenDays = (int)monitorEvent.Server.GameCode == 15;
            var shouldRegisterPage = (!supportsIw4 && !supportsSevenDays) || _addedPage;
            if (shouldRegisterPage)
            {
                return Task.CompletedTask;
            }

            (monitorEvent.Source as IManager)?.GetPageList().Pages
                .Add(Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_RADAR_TITLE"], "/radar");
            _addedPage = true;
        }

        return Task.CompletedTask;
    }
}
