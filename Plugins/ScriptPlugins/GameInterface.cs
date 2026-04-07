#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.3.22.1-preview

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using Game = SharedLibraryCore.Server.Game;

/// <summary>
/// Game Interface Plugin - Provides bidirectional communication between IW4MAdmin and
/// game server GSC scripts via dvars or file-based bus.
/// </summary>
public sealed class GameInterfacePlugin : IPluginV2
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<GameInterfaceConfig>(
            "GameInterfaceSettings",
            new GameInterfaceConfig());
        serviceCollection.AddSingleton<GameInterfaceState>();
    }

    public string Name => "Game Interface";
    public string Author => "RaidMax";
    public string Version => "2.1";

    private readonly ILogger<GameInterfacePlugin> _logger;
    private readonly GameInterfaceConfig _config;
    private readonly GameInterfaceState _state;
    private readonly ITranslationLookup _translationLookup;
    private readonly IManager _manager;
    private readonly IMetaServiceV2 _metaService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IScriptCommandFactory _scriptCommandFactory;

    private const string InDvar = "sv_iw4madmin_in";
    private const string OutDvar = "sv_iw4madmin_out";
    private const string IntegrationEnabledDvar = "sv_iw4madmin_integration_enabled";
    private const char GroupSeparator = '\x1d';
    private const char RecordSeparator = '\x1e';
    private const char UnitSeparator = '\x1f';

    public GameInterfacePlugin(
        ILogger<GameInterfacePlugin> logger,
        GameInterfaceConfig config,
        GameInterfaceState state,
        ITranslationLookup translationLookup,
        IManager manager,
        IMetaServiceV2 metaService,
        IDatabaseContextFactory contextFactory,
        IScriptCommandFactory scriptCommandFactory)
    {
        _logger = logger;
        _config = config;
        _state = state;
        _translationLookup = translationLookup;
        _manager = manager;
        _metaService = metaService;
        _contextFactory = contextFactory;
        _scriptCommandFactory = scriptCommandFactory;

        IManagementEventSubscriptions.ClientStateInitialized += OnClientEnteredMatch;
        IGameServerEventSubscriptions.ServerValueReceived += OnServerValueReceived;
        IGameServerEventSubscriptions.ServerValueSetCompleted += OnServerValueSetCompleted;
        IGameServerEventSubscriptions.MonitoringStarted += OnServerMonitoringStart;
        IGameServerEventSubscriptions.ServerRemoved += OnServerRemoved;
        IGameEventSubscriptions.MatchStarted += OnMatchStart;
        IManagementEventSubscriptions.ClientPenaltyAdministered += OnPenalty;

        _logger.LogInformation("[GameInterface] {Name} {Version} by {Author} loaded. PollingRate={PollingRate}ms",
            Name, Version, Author, _config.PollingRate);
    }

    private Task OnClientEnteredMatch(ClientStateInitializeEvent clientEvent, CancellationToken token)
    {
        var server = clientEvent.Client.CurrentServer;
        var serverState = _state.GetServerState(server.Id);

        _logger.LogDebug("[GameInterface] client entered match {Id}, currentState={@State}", server.Id, serverState);

        if (serverState is null)
        {
            InitializeServer(server);
        }
        else if (!serverState.Running && !serverState.InitializationInProgress)
        {
            _logger.LogDebug("[GameInterface] starting game interface loop");
            serverState.Running = true;
            RequestGetDvar(InDvar, server);
        }

        return Task.CompletedTask;
    }

    private Task OnPenalty(ClientPenaltyEvent penaltyEvent, CancellationToken token)
    {
        if (penaltyEvent.Penalty.Type != EFPenalty.PenaltyType.Warning || !penaltyEvent.Client.IsIngame)
        {
            return Task.CompletedTask;
        }

        SendScriptCommand(penaltyEvent.Client.CurrentServer, "Alert",
            penaltyEvent.Penalty.Punisher as EFClient, penaltyEvent.Client,
            new Dictionary<string, string>
            {
                ["alertType"] = (_translationLookup["GLOBAL_WARNING"] ?? "Warning") + "!",
                ["message"] = penaltyEvent.Penalty.Offense
            });

        return Task.CompletedTask;
    }

    private Task OnServerValueReceived(ServerValueReceiveEvent serverValueEvent, CancellationToken token)
    {
        switch (serverValueEvent.Response.Name)
        {
            case IntegrationEnabledDvar:
                HandleInitializeServerData(serverValueEvent);
                break;
            case InDvar:
                HandleIncomingServerData(serverValueEvent);
                break;
        }

        return Task.CompletedTask;
    }

    private async Task OnServerValueSetCompleted(ServerValueSetCompleteEvent serverValueEvent, CancellationToken token)
    {
        _logger.LogDebug("[GameInterface] set {DvarName}={DvarValue} success={Success} from {Server}",
            serverValueEvent.ValueName, serverValueEvent.Value, serverValueEvent.Success, serverValueEvent.Server.Id);

        if (serverValueEvent.ValueName is not InDvar and not OutDvar)
        {
            _logger.LogDebug("[GameInterface] ignoring set complete of {Name}", serverValueEvent.ValueName);
            return;
        }

        var serverState = _state.GetServerState(serverValueEvent.Server.Id);
        if (serverState is null)
        {
            _logger.LogDebug("[GameInterface] server {ServerId} was removed, ignoring value set complete",
                serverValueEvent.Server.Id);
            return;
        }

        serverState.OutQueue.TryDequeue(out _);

        _logger.LogDebug("[GameInterface] outQueue len = {OutLen}, inQueue len = {InLen}",
            serverState.OutQueue.Count, serverState.InQueue.Count);

        if (!serverValueEvent.Success && !_manager.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("[GameInterface] set of server value failed... retrying");
            RequestSetDvar(serverValueEvent.ValueName, serverValueEvent.Value, serverValueEvent.Server);
            return;
        }

        if (!serverState.InQueue.IsEmpty && serverValueEvent.ValueName == InDvar)
        {
            if (serverState.InQueue.TryDequeue(out var input))
            {
                try
                {
                    await ProcessEventMessageAsync(input, serverValueEvent.Server, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError("[GameInterface] could not process event message: {Exception}", ex.ToString());
                }
            }
        }

        _logger.LogDebug("[GameInterface] loop complete");
        RequestGetDvar(InDvar, serverValueEvent.Server);
    }

    private Task OnServerMonitoringStart(MonitorStartEvent monitorStartEvent, CancellationToken token)
    {
        InitializeServer(monitorStartEvent.Server);
        return Task.CompletedTask;
    }

    private Task OnServerRemoved(ServerRemoveEvent serverRemovedEvent, CancellationToken token)
    {
        var serverId = serverRemovedEvent.Server.Id;
        if (_state.GetServerState(serverId) is not null)
        {
            _logger.LogInformation("[GameInterface] cleaning up server state for removed server {ServerId}", serverId);
            _state.RemoveServerState(serverId);
        }

        return Task.CompletedTask;
    }

    private Task OnMatchStart(MatchStartEvent matchStartEvent, CancellationToken token)
    {
        _state.BusMode = "rcon";
        QueueEventMessage(matchStartEvent.Server, true, "GetBusModeRequested", null, null, null,
            new Dictionary<string, string>());
        return Task.CompletedTask;
    }

    private void InitializeServer(IGameServer server)
    {
        _state.SetServerState(server.Id, new ServerState());
        _logger.LogDebug("[GameInterface] initializing game interface for {ServerId}", server.Id);
        RequestGetDvar(IntegrationEnabledDvar, server);
    }

    private void HandleInitializeServerData(ServerValueReceiveEvent responseEvent)
    {
        var serverState = _state.GetServerState(responseEvent.Server.Id);
        if (serverState is null)
        {
            _logger.LogDebug("[GameInterface] server {ServerId} was removed, ignoring initialization",
                responseEvent.Server.Id);
            return;
        }

        if (responseEvent.Response.Value != "1")
        {
            _logger.LogInformation("[GameInterface] gsc integration is disabled for {Server}",
                responseEvent.Server.Id);
            return;
        }

        _logger.LogInformation("[GameInterface] gsc integration is enabled for {Server}",
            responseEvent.Server.Id);

        serverState.OutQueue.TryDequeue(out _);
        serverState.Enabled = true;
        serverState.Running = true;
        serverState.InitializationInProgress = false;

        // todo: this might not work for all games
        responseEvent.Server.RconParser.Configuration.FloodProtectInterval = 150;

        QueueEventMessage(responseEvent.Server, true, "GetBusModeRequested", null, null, null,
            new Dictionary<string, string>());
        QueueEventMessage(responseEvent.Server, true, "GetCommandsRequested", null, null, null,
            new Dictionary<string, string>());
        RequestGetDvar(InDvar, responseEvent.Server);
    }

    private void HandleIncomingServerData(ServerValueReceiveEvent responseEvent)
    {
        _logger.LogDebug("[GameInterface] received {DvarName}={DvarValue} success={Success} from {Server}",
            responseEvent.Response.Name, responseEvent.Response.Value, responseEvent.Success,
            responseEvent.Server.Id);

        var serverState = _state.GetServerState(responseEvent.Server.Id);
        if (serverState is null)
        {
            _logger.LogDebug("[GameInterface] server {ServerId} was removed, ignoring incoming data",
                responseEvent.Server.Id);
            return;
        }

        serverState.OutQueue.TryDequeue(out _);

        if (responseEvent.Server.ConnectedClients.Count == 0 && !Utilities.IsDevelopment)
        {
            _logger.LogDebug("[GameInterface] stopping game interface loop");
            serverState.Running = false;
            return;
        }

        if (!responseEvent.Success && !_manager.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("[GameInterface] get of server value failed... retrying");
            RequestGetDvar(responseEvent.Response.Name, responseEvent.Server);
            return;
        }

        if (_manager.CancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("[GameInterface] shutdown requested so we are ending loop");
            return;
        }

        var input = responseEvent.Response.Value;
        var server = responseEvent.Server;

        if (string.IsNullOrEmpty(input) || input == "null")
        {
            _logger.LogDebug("[GameInterface] no data to process from server");

            if (serverState.CommandQueue.TryDequeue(out var nextMessage))
            {
                _logger.LogDebug("[GameInterface] sending next out message");
                RequestSetDvar(OutDvar, nextMessage, server);
            }
            else
            {
                RequestGetDvar(InDvar, server);
            }

            return;
        }

        serverState.InQueue.Enqueue(input);
        RequestSetDvar(InDvar, "", server);
    }

    private async Task ProcessEventMessageAsync(string input, IGameServer server, CancellationToken token)
    {
        var eventData = ParseEvent(input);

        _logger.LogDebug("[GameInterface] processing input... {EventType} {SubType} {@Data} {ClientNumber}",
            eventData.EventType, eventData.SubType, eventData.Data, eventData.ClientNumber);

        switch (eventData.EventType)
        {
            case "ClientDataRequested":
                await HandleClientDataRequestedAsync(eventData, server, token);
                break;
            case "SetClientDataRequested":
                await HandleSetClientDataRequestedAsync(eventData, server, token);
                break;
            case "UrlRequested":
                await HandleUrlRequestedAsync(eventData, server, token);
                break;
            case "RegisterCommandRequested":
                HandleRegisterCommandRequested(eventData);
                break;
            case "GetBusModeRequested":
                HandleGetBusModeRequested(eventData);
                break;
        }

        _logger.LogDebug("[GameInterface] finished processing input for {Type}", eventData.EventType);
    }

    private async Task HandleClientDataRequestedAsync(GameInterfaceEvent eventData, IGameServer server,
        CancellationToken token)
    {
        var clientNumber = int.TryParse(eventData.ClientNumber, out var cn) ? cn : -1;
        var client = server.ConnectedClients.FirstOrDefault(c => c.ClientNumber == clientNumber);

        if (client is null)
        {
            _logger.LogWarning("[GameInterface] could not find client slot {ClientNumber} when processing {EventType}",
                eventData.ClientNumber, eventData.EventType);
            QueueEventMessage(server, false, "ClientDataReceived", "Fail", null, null,
                new Dictionary<string, string> { ["ClientNumber"] = eventData.ClientNumber ?? "" });
            return;
        }

        _logger.LogDebug("[GameInterface] Found client {Name}", client.Name);

        Dictionary<string, string> data;

        if (eventData.SubType == "Meta")
        {
            var metaKey = eventData.Data?.ToString() ?? "";
            var meta = await _metaService.GetPersistentMeta(metaKey, client.ClientId, token);
            data = new Dictionary<string, string> { [metaKey] = meta?.Value ?? "" };
            _logger.LogDebug("[GameInterface] event data is {Data}", metaKey);
        }
        else
        {
            var clientStats = await GetClientStatsAsync(client.ClientId, server.LegacyDatabaseId, token);
            var tagMeta = await _metaService.GetPersistentMetaByLookup(
                "ClientTagV2", "ClientTagNameV2", client.ClientId, token);

            data = new Dictionary<string, string>
            {
                ["level"] = client.Level.ToString(),
                ["clientId"] = client.ClientId.ToString(CultureInfo.InvariantCulture),
                ["lastConnection"] = client.TimeSinceLastConnectionString,
                ["tag"] = tagMeta?.Value ?? "",
                ["performance"] = (clientStats?.Performance ?? 200.0).ToString("F1", CultureInfo.InvariantCulture),
                ["ipAddress"] = client.IPAddressString
            };
        }

        QueueEventMessage(server, false, "ClientDataReceived", eventData.SubType, client, null, data);
    }

    private async Task HandleSetClientDataRequestedAsync(GameInterfaceEvent eventData, IGameServer server,
        CancellationToken token)
    {
        var clientNumber = int.TryParse(eventData.ClientNumber, out var cn) ? cn : -1;
        var client = server.ConnectedClients.FirstOrDefault(c => c.ClientNumber == clientNumber);
        var dataDict = eventData.Data as Dictionary<string, string?>;

        var clientId = client?.ClientId
                       ?? (dataDict is not null
                           && dataDict.TryGetValue("clientId", out var idStr)
                           && int.TryParse(idStr, out var parsed)
                               ? parsed
                               : -1);

        _logger.LogDebug("[GameInterface] clientId={ClientId}", clientId);

        if (clientId < 0)
        {
            _logger.LogWarning(
                "[GameInterface] could not find client slot {ClientNumber} when processing {EventType}: {EventData}",
                eventData.ClientNumber, eventData.EventType, eventData.Data);
            QueueEventMessage(server, false, "SetClientDataCompleted", "Meta", null, null,
                new Dictionary<string, string>
                {
                    ["ClientNumber"] = eventData.ClientNumber ?? "",
                    ["status"] = "Fail"
                });
            return;
        }

        if (eventData.SubType != "Meta" || dataDict is null
                                        || !dataDict.TryGetValue("value", out var value)
                                        || !dataDict.TryGetValue("key", out var key))
        {
            return;
        }
        var status = "Complete";

        try
        {
            _logger.LogDebug("[GameInterface] key={Key}, value={Value}, direction={Direction}",
                key, value, dataDict.GetValueOrDefault("direction"));

            if (dataDict.TryGetValue("direction", out var direction) && direction is not null)
            {
                if (int.TryParse(value, out var parsedValue))
                {
                    if (direction == "increment")
                        await _metaService.IncrementPersistentMeta(key, parsedValue, clientId, token);
                    else
                        await _metaService.DecrementPersistentMeta(key, parsedValue, clientId, token);
                }
            }
            else
            {
                await _metaService.SetPersistentMeta(key, value, clientId, token);
            }

            if (key == "PersistentClientGuid" && client is not null)
            {
                _manager.QueueEvent(new ClientPersistentIdReceiveEvent(client, value));
            }
        }
        catch (Exception error)
        {
            status = "Fail";
            _logger.LogError("[GameInterface] could not persist client meta {Key}={Value} {Error} for {Client}",
                key, value, error.ToString(), clientId);
        }

        QueueEventMessage(server, false, "SetClientDataCompleted", "Meta", null, null,
            new Dictionary<string, string>
            {
                ["ClientNumber"] = eventData.ClientNumber ?? "",
                ["status"] = status
            });
    }

    private async Task HandleUrlRequestedAsync(GameInterfaceEvent eventData, IGameServer server,
        CancellationToken token)
    {
        var dataDict = eventData.Data as Dictionary<string, string>;
        if (dataDict is null || !dataDict.TryGetValue("url", out var url) || string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("[GameInterface] no url provided for gamescript web request - {@Event}", eventData);
            return;
        }

        var method = dataDict.GetValueOrDefault("method") ?? "GET";
        var contentType = dataDict.GetValueOrDefault("contentType") ?? "text/plain";
        var body = dataDict.GetValueOrDefault("body");
        var headerString = dataDict.GetValueOrDefault("headers");

        _logger.LogDebug("[GameInterface] making gamescript web request {Url} {Method}", url, method);

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (headerString is not null)
            {
                foreach (var header in headerString.Split(','))
                {
                    var kv = header.Split(':');
                    if (kv.Length == 2)
                        request.Headers.TryAddWithoutValidation(kv[0].Trim(), kv[1].Trim());
                }
            }

            if (body is not null)
                request.Content = new StringContent(body, Encoding.UTF8, contentType);

            using var response = await HttpClient.SendAsync(request, token);
            var responseString = await response.Content.ReadAsStringAsync(token);

            _logger.LogDebug("[GameInterface] got response for gamescript web request - length={Length}",
                responseString.Length);

            var quoteReplace = server.GameCode == Reference.Game.T6 ? "\\\\\\\"" : "\\\"";

            var cleaned = responseString
                .Replace("\"", quoteReplace, StringComparison.Ordinal)
                .Replace("\n", "", StringComparison.Ordinal)
                .Replace("\t", "", StringComparison.Ordinal);

            const int chunkSize = 800;
            const int maxChunks = 10;
            var chunks = ChunkString(cleaned, chunkSize);

            if (chunks.Count > maxChunks)
            {
                _logger.LogWarning("[GameInterface] response chunks greater than max ({Max}). Data truncated!",
                    maxChunks);
                chunks = chunks.Take(maxChunks).ToList();
            }

            var entity = dataDict.GetValueOrDefault("entity") ?? "";
            for (var i = 0; i < chunks.Count; i++)
            {
                QueueEventMessage(server, false, "UrlRequestCompleted", null, null, null,
                    new Dictionary<string, string>
                    {
                        ["entity"] = entity,
                        ["remaining"] = (chunks.Count - (i + 1)).ToString(CultureInfo.InvariantCulture),
                        ["response"] = chunks[i]
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("[GameInterface] web request failed: {Exception}", ex.ToString());
        }
    }

    private void HandleRegisterCommandRequested(GameInterfaceEvent eventData)
    {
        if (eventData.Data is not Dictionary<string, string> dataDict) return;

        var commandName = dataDict.GetValueOrDefault("name") ?? "DEFAULT";
        var description = dataDict.GetValueOrDefault("description") ?? "DEFAULT";
        var alias = dataDict.GetValueOrDefault("alias") ?? "DEFAULT";
        var permission = dataDict.GetValueOrDefault("minPermission") ?? "User";
        var targetRequired = (dataDict.GetValueOrDefault("targetRequired") ?? "0") == "1";
        var supportedGamesStr = dataDict.GetValueOrDefault("supportedGames") ?? "";
        var eventKey = dataDict.GetValueOrDefault("eventKey") ?? commandName;

        var supportedGames = supportedGamesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(g => Enum.TryParse<Reference.Game>(g.Trim(), out var game) ? game : (Reference.Game?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value);

        var plugin = this;

        Task ExecuteAction(GameEvent gameEvent)
        {
            if (!plugin.ValidateEnabled(gameEvent.Owner, gameEvent.Origin)) return Task.CompletedTask;

            if (gameEvent.Data == "--reload" && gameEvent.Origin.Level == Data.Models.Client.EFClient.Permission.Owner)
            {
                plugin.QueueEventMessage(gameEvent.Owner, true, "GetCommandsRequested", null, null, null,
                    new Dictionary<string, string> { ["name"] = gameEvent.Extra?.ToString() ?? "" });
            }
            else
            {
                plugin.SendScriptCommand(gameEvent.Owner, $"{eventKey}Execute", gameEvent.Origin, gameEvent.Target,
                    new Dictionary<string, string> { ["args"] = gameEvent.Data ?? "" });
            }

            return Task.CompletedTask;
        }

        var scriptCommand = _scriptCommandFactory.CreateScriptCommand(
            commandName, alias, description, permission, targetRequired,
            [], ExecuteAction, supportedGames);

        _manager.RemoveCommandByName(scriptCommand.Name);
        _manager.AddAdditionalCommand(scriptCommand);
        _logger.LogDebug("[GameInterface] registered dynamic command {Name}", commandName);
    }

    private void HandleGetBusModeRequested(GameInterfaceEvent eventData)
    {
        if (eventData.Data is not Dictionary<string, string> dataDict) return;
        if (!dataDict.TryGetValue("directory", out var directory) || !dataDict.TryGetValue("mode", out var mode))
            return;

        _state.BusMode = mode;
        _state.BusDir = directory.Replace("'", "", StringComparison.Ordinal)
            .Replace("\"", "", StringComparison.Ordinal);

        if (_state.BusMode == "file"
            && dataDict.TryGetValue("inLocation", out var inLoc)
            && dataDict.TryGetValue("outLocation", out var outLoc))
        {
            _state.BusFileIn = inLoc;
            _state.BusFileOut = outLoc;
        }

        _logger.LogDebug("[GameInterface] setting bus mode to {Mode} dir={Dir}", _state.BusMode, _state.BusDir);
    }

    private void QueueEventMessage(IGameServer server, bool responseExpected, string eventType,
        string? subType, EFClient? origin, EFClient? target, Dictionary<string, string> data)
    {
        var serverState = _state.GetServerState(server.Id);
        if (serverState is null)
        {
            _logger.LogDebug("[GameInterface] skipping QueueEventMessage for removed server {Id}", server.Id);
            return;
        }

        var output = FormatEventMessage(responseExpected, eventType, subType,
            origin?.ClientNumber ?? -1, target?.ClientNumber ?? -1, data);

        _logger.LogDebug("[GameInterface] queuing output for server {Output}", output);
        serverState.CommandQueue.Enqueue(output);
    }

    private void SendScriptCommand(IGameServer server, string command, EFClient? origin, EFClient? target,
        Dictionary<string, string>? data)
    {
        var serverState = _state.GetServerState(server.Id);
        if (serverState is not { Enabled: true }) return;

        QueueEventMessage(server, false, "ExecuteCommandRequested", command, origin, target,
            data ?? new Dictionary<string, string>());
    }

    private int CalculateDelay(IGameServer server)
    {
        var delayMs = _config.PollingRate;

        if (server.MatchEndTime is not null)
        {
            const int extraDelay = 15000;
            var diff = (DateTime.UtcNow - server.MatchEndTime.Value).TotalMilliseconds;
            if (diff < extraDelay)
            {
                delayMs = (int)(extraDelay - diff) + _config.PollingRate;
                _logger.LogDebug("[GameInterface] increasing delay to {Delay}ms due to recent map change", delayMs);
            }
        }

        return delayMs;
    }

    private void RequestGetDvar(string dvarName, IGameServer server)
    {
        var serverState = _state.GetServerState(server.Id);
        if (serverState is null)
        {
            _logger.LogDebug("[GameInterface] skipping requestGetDvar for removed server {Id}", server.Id);
            return;
        }

        if (dvarName != IntegrationEnabledDvar && _state.BusMode == "file")
        {
            _ = ExecuteFileBusGetAsync(dvarName, server, serverState);
            return;
        }

        var requestEvent = new ServerValueRequestEvent(dvarName, server)
        {
            DelayMs = CalculateDelay(server),
            TimeoutMs = 2000,
            Source = Name
        };

        _logger.LogDebug("[GameInterface] requesting {Dvar}", dvarName);
        serverState.OutQueue.Enqueue(requestEvent);

        if (serverState.OutQueue.Count <= 1)
            _manager.QueueEvent(requestEvent);
        else
            _logger.LogError("[GameInterface::requestGetDvar] queue is full!");
    }

    private void RequestSetDvar(string dvarName, string dvarValue, IGameServer server)
    {
        var serverState = _state.GetServerState(server.Id);
        if (serverState is null)
        {
            _logger.LogDebug("[GameInterface] skipping requestSetDvar for removed server {Id}", server.Id);
            return;
        }

        if (_state.BusMode == "file")
        {
            _ = ExecuteFileBusSetAsync(dvarName, dvarValue, server, serverState);
            return;
        }

        var requestEvent = new ServerValueSetRequestEvent(dvarName, dvarValue, server)
        {
            DelayMs = CalculateDelay(server),
            TimeoutMs = 2000,
            Source = Name
        };

        serverState.OutQueue.Enqueue(requestEvent);
        _logger.LogDebug("[GameInterface] outQueue size = {Length}", serverState.OutQueue.Count);

        if (serverState.OutQueue.Count == 1)
            _manager.QueueEvent(requestEvent);
        else
            _logger.LogError("[GameInterface::requestSetDvar] queue is full!");
    }

    private async Task ExecuteFileBusGetAsync(string dvarName, IGameServer server, ServerState serverState)
    {
        await Task.Delay(250, _manager.CancellationToken);
        serverState.OutQueue.Enqueue(new object());

        try
        {
            var content = await File.ReadAllTextAsync(
                Path.Combine(_state.BusDir, FileForDvar(dvarName)), _manager.CancellationToken);

            await OnServerValueReceived(new ServerValueReceiveEvent
            {
                Server = server, Source = server, Success = true,
                Response = new Dvar<string> { Name = dvarName, Value = content }
            }, _manager.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError("[GameInterface] could not get bus data {Exception}", e.ToString());
            await OnServerValueReceived(new ServerValueReceiveEvent
            {
                Server = server, Success = false,
                Response = new Dvar<string> { Name = dvarName }
            }, _manager.CancellationToken);
        }
    }

    private async Task ExecuteFileBusSetAsync(string dvarName, string dvarValue, IGameServer server,
        ServerState serverState)
    {
        await Task.Delay(250, _manager.CancellationToken);

        try
        {
            var path = Path.Combine(_state.BusDir, FileForDvar(dvarName));
            _logger.LogDebug("[GameInterface] writing {Value} to {File}", dvarValue, path);
            await File.WriteAllTextAsync(path, dvarValue, _manager.CancellationToken);

            serverState.OutQueue.Enqueue(new object());
            await OnServerValueSetCompleted(new ServerValueSetCompleteEvent
            {
                Server = server, Source = server, Success = true,
                Value = dvarValue, ValueName = dvarName
            }, _manager.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError("[GameInterface] could not set bus data {Exception}", e.ToString());
            await OnServerValueSetCompleted(new ServerValueSetCompleteEvent
            {
                Server = server, Success = false,
                ValueName = dvarName, Value = dvarValue
            }, _manager.CancellationToken);
        }
    }

    private async Task<EFClientStatistics?> GetClientStatsAsync(int clientId, long serverId, CancellationToken token)
    {
        try
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.ClientStatistics
                .FirstOrDefaultAsync(s => s.ClientId == clientId && s.ServerId == serverId, token);
        }
        catch (Exception ex)
        {
            _logger.LogError("[GameInterface] failed to get client stats: {Exception}", ex.ToString());
            return null;
        }
    }

    private bool ValidateEnabled(IGameServer server, EFClient origin)
    {
        var enabled = _state.GetServerState(server.Id) is { Enabled: true };
        if (!enabled)
            origin.Tell("Game interface is not enabled on this server");
        return enabled;
    }

    private string FileForDvar(string dvar) => dvar == InDvar ? _state.BusFileIn : _state.BusFileOut;

    public void Dispose()
    {
        IManagementEventSubscriptions.ClientStateInitialized -= OnClientEnteredMatch;
        IGameServerEventSubscriptions.ServerValueReceived -= OnServerValueReceived;
        IGameServerEventSubscriptions.ServerValueSetCompleted -= OnServerValueSetCompleted;
        IGameServerEventSubscriptions.MonitoringStarted -= OnServerMonitoringStart;
        IGameServerEventSubscriptions.ServerRemoved -= OnServerRemoved;
        IGameEventSubscriptions.MatchStarted -= OnMatchStart;
        IManagementEventSubscriptions.ClientPenaltyAdministered -= OnPenalty;
        _logger.LogInformation("[GameInterface] Game Interface unloaded");
    }

    #region Message Formatting / Parsing

    internal static string FormatEventMessage(bool responseExpected, string eventType, string? subType,
        int originClientNumber, int targetClientNumber, Dictionary<string, string>? data)
    {
        return $"{(responseExpected ? '1' : '0')}{GroupSeparator}{eventType}{GroupSeparator}" +
               $"{subType}{GroupSeparator}{originClientNumber}{GroupSeparator}" +
               $"{targetClientNumber}{GroupSeparator}{BuildDataString(data)}";
    }

    private static string BuildDataString(Dictionary<string, string>? data)
    {
        if (data is null || data.Count == 0) return "";

        var sb = new StringBuilder();
        var first = true;
        foreach (var (key, value) in data)
        {
            if (!first) sb.Append(RecordSeparator);
            sb.Append(key);
            sb.Append(UnitSeparator);
            sb.Append(value);
            first = false;
        }

        return sb.ToString();
    }

    private static GameInterfaceEvent ParseEvent(string input)
    {
        if (string.IsNullOrEmpty(input)) return new GameInterfaceEvent();

        var parts = input.Split(GroupSeparator);
        return new GameInterfaceEvent
        {
            EventType = parts.Length > 1 ? parts[1] : null,
            SubType = parts.Length > 2 ? parts[2] : null,
            ClientNumber = parts.Length > 3 ? parts[3] : null,
            Data = parts.Length > 4 ? ParseDataString(parts[4]) : null
        };
    }

    private static object? ParseDataString(string data)
    {
        if (string.IsNullOrEmpty(data)) return data;

        var dict = new Dictionary<string, string>();
        foreach (var segment in data.Split(RecordSeparator))
        {
            var kv = segment.Split(UnitSeparator);
            if (kv.Length == 2)
                dict[kv[0]] = kv[1];
        }

        return dict.Count == 0 ? data : dict;
    }

    private static List<string> ChunkString(string str, int chunkSize)
    {
        var result = new List<string>((str.Length / chunkSize) + 1);
        for (var i = 0; i < str.Length; i += chunkSize)
            result.Add(str.Substring(i, Math.Min(chunkSize, str.Length - i)));
        return result;
    }

    #endregion
}

#region Supporting Types

public class GameInterfaceState
{
    private readonly ConcurrentDictionary<string, ServerState> _servers = new();

    public string BusMode { get; set; } = "rcon";
    public string BusDir { get; set; } = "";
    public string BusFileIn { get; set; } = "";
    public string BusFileOut { get; set; } = "";

    public ServerState? GetServerState(string serverId) =>
        _servers.TryGetValue(serverId, out var state) ? state : null;

    public void SetServerState(string serverId, ServerState state) => _servers[serverId] = state;
    public void RemoveServerState(string serverId) => _servers.TryRemove(serverId, out _);
}

public class ServerState
{
    public bool Enabled { get; set; }
    public bool Running { get; set; }
    public bool InitializationInProgress { get; set; } = true;
    public ConcurrentQueue<string> InQueue { get; } = new();
    public ConcurrentQueue<object> OutQueue { get; } = new();
    public ConcurrentQueue<string> CommandQueue { get; } = new();
}

public class GameInterfaceEvent
{
    public string? EventType { get; init; }
    public string? SubType { get; init; }
    public string? ClientNumber { get; init; }
    public object? Data { get; init; }
}

public class GameInterfaceConfig
{
    public int PollingRate { get; set; } = 300;
}

#endregion

#region Commands

/// <summary>
/// Base class for all game interface commands. Handles validation and
/// script command dispatch through the shared <see cref="GameInterfaceState"/>.
/// </summary>
public abstract class GameInterfaceCommand(
    CommandConfiguration config,
    ITranslationLookup translationLookup,
    GameInterfaceState state)
    : Command(config, translationLookup)
{
    protected bool ValidateEnabled(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        var enabled = state.GetServerState(gameEvent.Owner.Id) is { Enabled: true };
        if (!enabled)
            gameEvent.Origin.Tell("Game interface is not enabled on this server");
        return enabled;
    }

    protected void SendScriptCommand(IGameServer server, string command,
        EFClient? origin, EFClient? target, Dictionary<string, string>? data)
    {
        ArgumentNullException.ThrowIfNull(server);
        var serverState = state.GetServerState(server.Id);
        if (serverState is not { Enabled: true }) return;

        var output = GameInterfacePlugin.FormatEventMessage(false, "ExecuteCommandRequested", command,
            origin?.ClientNumber ?? -1, target?.ClientNumber ?? -1, data);
        serverState.CommandQueue.Enqueue(output);
    }

    protected static readonly Game[] AllScriptGames = [Game.IW4, Game.IW5, Game.T4, Game.T5, Game.T6];
}

public class GiveWeaponCommand : GameInterfaceCommand
{
    public GiveWeaponCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "giveweapon";
        Description = "gives specified weapon";
        Alias = "gw";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }, new CommandArgument { Name = "weapon name", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "GiveWeapon", gameEvent.Origin, gameEvent.Target,
            new Dictionary<string, string> { ["weaponName"] = gameEvent.Data });
        return Task.CompletedTask;
    }
}

public class TakeWeaponsCommand : GameInterfaceCommand
{
    public TakeWeaponsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "takeweapons";
        Description = "take all weapons from specified player";
        Alias = "tw";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "TakeWeapons", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

public class SwitchTeamCommand : GameInterfaceCommand
{
    public SwitchTeamCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "switchteam";
        Description = "switches specified player to the opposite team";
        Alias = "st";
        Permission = Data.Models.Client.EFClient.Permission.Administrator;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "SwitchTeams", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

public class LockControlsCommand : GameInterfaceCommand
{
    public LockControlsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "lockcontrols";
        Description = "locks target player's controls";
        Alias = "lc";
        Permission = Data.Models.Client.EFClient.Permission.Administrator;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "LockControls", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

public class NoClipCommand : GameInterfaceCommand
{
    public NoClipCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "noclip";
        Description = "enable noclip on yourself ingame";
        Alias = "nc";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = false;
        SupportedGames = [Game.IW4, Game.IW5];
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "NoClip", gameEvent.Origin, gameEvent.Origin, null);
        return Task.CompletedTask;
    }
}

public class HideCommand : GameInterfaceCommand
{
    public HideCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "hide";
        Description = "hide yourself ingame";
        Alias = "hi";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = false;
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "Hide", gameEvent.Origin, gameEvent.Origin, null);
        return Task.CompletedTask;
    }
}

public class AlertCommand : GameInterfaceCommand
{
    public AlertCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "alert";
        Description = "alert a player";
        Alias = "alr";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }, new CommandArgument { Name = "message", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "Alert", gameEvent.Origin, gameEvent.Target,
            new Dictionary<string, string> { ["alertType"] = "Alert", ["message"] = gameEvent.Data });
        return Task.CompletedTask;
    }
}

public class GotoPlayerCommand : GameInterfaceCommand
{
    public GotoPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "gotoplayer";
        Description = "teleport to a player";
        Alias = "g2p";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "Goto", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

public class PlayerToMeCommand : GameInterfaceCommand
{
    public PlayerToMeCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "playertome";
        Description = "teleport a player to you";
        Alias = "p2m";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "PlayerToMe", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

public class GotoCommand : GameInterfaceCommand
{
    public GotoCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "goto";
        Description = "teleport to a position";
        Alias = "g2";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument { Name = "x", Required = true },
            new CommandArgument { Name = "y", Required = true },
            new CommandArgument { Name = "z", Required = true }
        ];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;

        var args = (gameEvent.Data ?? "").Split(' ');
        SendScriptCommand(gameEvent.Owner, "Goto", gameEvent.Origin, gameEvent.Target,
            new Dictionary<string, string>
            {
                ["x"] = args.ElementAtOrDefault(0) ?? "0",
                ["y"] = args.ElementAtOrDefault(1) ?? "0",
                ["z"] = args.ElementAtOrDefault(2) ?? "0"
            });
        return Task.CompletedTask;
    }
}

public class KillPlayerCommand : GameInterfaceCommand
{
    public KillPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "kill";
        Description = "kill a player";
        Alias = "kpl";
        Permission = Data.Models.Client.EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "Kill", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

public class SetSpectatorCommand : GameInterfaceCommand
{
    public SetSpectatorCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        GameInterfaceState state) : base(config, translationLookup, state)
    {
        Name = "setspectator";
        Description = "sets a player as spectator";
        Alias = "spec";
        Permission = Data.Models.Client.EFClient.Permission.Administrator;
        RequiresTarget = true;
        Arguments = [new CommandArgument { Name = "player", Required = true }];
        SupportedGames = AllScriptGames;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!ValidateEnabled(gameEvent)) return Task.CompletedTask;
        SendScriptCommand(gameEvent.Owner, "SetSpectator", gameEvent.Origin, gameEvent.Target, null);
        return Task.CompletedTask;
    }
}

#endregion
