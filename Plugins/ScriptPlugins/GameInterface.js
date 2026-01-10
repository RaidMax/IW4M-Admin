const servers = {};
const inDvar = 'sv_iw4madmin_in';
const outDvar = 'sv_iw4madmin_out';
const integrationEnabledDvar = 'sv_iw4madmin_integration_enabled';
const groupSeparatorChar = '\x1d';
const recordSeparatorChar = '\x1e';
const unitSeparatorChar = '\x1f';

let busFileIn = '';
let busFileOut = '';
let busMode = 'rcon';
let busDir = '';

const init = (registerNotify, serviceResolver, config, scriptHelper) => {
    registerNotify('IManagementEventSubscriptions.ClientStateInitialized', (clientEvent, _) => plugin.onClientEnteredMatch(clientEvent));
    registerNotify('IGameServerEventSubscriptions.ServerValueReceived', (serverValueEvent, _) => plugin.onServerValueReceived(serverValueEvent));
    registerNotify('IGameServerEventSubscriptions.ServerValueSetCompleted', (serverValueEvent, _) => plugin.onServerValueSetCompleted(serverValueEvent));
    registerNotify('IGameServerEventSubscriptions.MonitoringStarted', (monitorStartEvent, _) => plugin.onServerMonitoringStart(monitorStartEvent));
    registerNotify('IGameServerEventSubscriptions.ServerRemoved', (serverRemovedEvent, _) => plugin.onServerRemoved(serverRemovedEvent));
    registerNotify('IGameEventSubscriptions.MatchStarted', (matchStartEvent, _) => plugin.onMatchStart(matchStartEvent));
    registerNotify('IManagementEventSubscriptions.ClientPenaltyAdministered', (penaltyEvent, _) => plugin.onPenalty(penaltyEvent));

    plugin.onLoad(serviceResolver, config, scriptHelper);
    return plugin;
};

const plugin = {
    author: 'RaidMax',
    version: '2.1',
    name: 'Game Interface',
    serviceResolver: null,
    eventManager: null,
    logger: null,
    commands: null,
    scriptHelper: null,
    configWrapper: null,
    config: {
        pollingRate: 300
    },

    onLoad: function (serviceResolver, configWrapper, scriptHelper) {
        this.serviceResolver = serviceResolver;
        this.eventManager = serviceResolver.resolveService('IManager');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.commands = commands;
        this.configWrapper = configWrapper;
        this.scriptHelper = scriptHelper;

        const storedConfig = this.configWrapper.getValue('config', newConfig => {
            if (newConfig) {
                plugin.logger.logInformation('[GameInterface] {Name} config reloaded. Polling rate is {Rate}', plugin.name, newConfig.pollingRate);
                plugin.config = newConfig;
            }
        });

        if (storedConfig != null) {
            this.config = storedConfig
        } else {
            this.configWrapper.setValue('config', this.config);
        }
    },

    onClientEnteredMatch: function (clientEvent) {
        const serverState = servers[clientEvent.client.currentServer.id];
        this.logger.logDebug('[GameInterface] client entered match {id}, currentState={@state}', clientEvent.client.currentServer.id, serverState);

        if (serverState === undefined || serverState == null) {
            this.initializeServer(clientEvent.client.currentServer);
        } else if (!serverState.running && !serverState.initializationInProgress) {
            this.logger.logDebug('[GameInterface] starting game interface loop');
            serverState.running = true;
            this.requestGetDvar(inDvar, clientEvent.client.currentServer);
        }
    },

    onPenalty: function (penaltyEvent) {
        const warning = 1;
        if (penaltyEvent.penalty.type !== warning || !penaltyEvent.client.isIngame) {
            return;
        }

        sendScriptCommand(penaltyEvent.client.currentServer, 'Alert', penaltyEvent.penalty.punisher, penaltyEvent.client, {
            alertType: this.translations('GLOBAL_WARNING') + '!',
            message: penaltyEvent.penalty.offense
        });
    },

    onServerValueReceived: function (serverValueEvent) {
        const name = serverValueEvent.response.name;
        if (name === integrationEnabledDvar) {
            this.handleInitializeServerData(serverValueEvent);
        } else if (name === inDvar) {
            this.handleIncomingServerData(serverValueEvent);
        }
    },

    onServerValueSetCompleted: function (serverValueEvent) {
        this.logger.logDebug('[GameInterface] set {dvarName}={dvarValue} success={success} from {server}', serverValueEvent.valueName,
            serverValueEvent.value, serverValueEvent.success, serverValueEvent.server.id);

        if (serverValueEvent.valueName !== inDvar && serverValueEvent.valueName !== outDvar) {
            this.logger.logDebug('[GameInterface] ignoring set complete of {name}', serverValueEvent.valueName);
            return;
        }

        const serverState = servers[serverValueEvent.server.id];
        // Server may have been removed - gracefully exit
        if (!serverState) {
            this.logger.logDebug('[GameInterface] server {serverId} was removed, ignoring value set complete', serverValueEvent.server.id);
            return;
        }
        serverState.outQueue.shift();

        this.logger.logDebug('[GameInterface] outQueue len = {outLen}, inQueue len = {inLen}', serverState.outQueue.length, serverState.inQueue.length);

        // if it didn't succeed, we need to retry
        if (!serverValueEvent.success && !this.eventManager.cancellationToken.isCancellationRequested) {
            this.logger.logWarning('[GameInterface] set of server value failed... retrying');
            this.requestSetDvar(serverValueEvent.valueName, serverValueEvent.value, serverValueEvent.server);
            return;
        }

        // we informed the server that we received the event
        if (serverState.inQueue.length > 0 && serverValueEvent.valueName === inDvar) {
            const input = serverState.inQueue.shift();

            // if we queued an event then the next loop will be at the value set complete
            try {
                this.processEventMessage(input, serverValueEvent.server);
            } catch (ex) {
                this.logger.logError('[GameInterface] could not process event message: {exception}', ex.toString());
            }
        }

        this.logger.logDebug('[GameInterface] loop complete');
        // loop restarts
        this.requestGetDvar(inDvar, serverValueEvent.server);
    },

    onServerMonitoringStart: function (monitorStartEvent) {
        this.initializeServer(monitorStartEvent.server);
    },

    onServerRemoved: function (serverRemovedEvent) {
        const serverId = serverRemovedEvent.server.id;
        if (servers[serverId]) {
            this.logger.logInformation('[GameInterface] cleaning up server state for removed server {serverId}', serverId);
            // Stop any running loops
            if (servers[serverId].running) {
                servers[serverId].running = false;
            }
            // Clear queues
            servers[serverId].inQueue = [];
            servers[serverId].outQueue = [];
            servers[serverId].commandQueue = [];
            // Remove from dictionary
            delete servers[serverId];
        }
    },

    onMatchStart: function (matchStartEvent) {
        busMode = 'rcon';
        this.sendEventMessage(matchStartEvent.server, true, 'GetBusModeRequested', null, null, null, {});
    },

    initializeServer: function (server) {
        servers[server.id] = {
            enabled: false,
            running: false,
            initializationInProgress: true,
            inQueue: [],
            outQueue: [],
            commandQueue: []
        };

        this.logger.logDebug('[GameInterface] initializing game interface for {serverId}', server.id);
        this.requestGetDvar(integrationEnabledDvar, server);
    },

    handleInitializeServerData: function (responseEvent) {
        const serverState = servers[responseEvent.server.id];
        // Server may have been removed - gracefully exit
        if (!serverState) {
            this.logger.logDebug('[GameInterface] server {serverId} was removed, ignoring initialization', responseEvent.server.id);
            return;
        }

        if (responseEvent.response.value !== '1') {
            this.logger.logInformation('[GameInterface] gsc integration is disabled for {server}', responseEvent.server.id);
            return;
        }

        this.logger.logInformation('[GameInterface] gsc integration is enabled for {server}', responseEvent.server.id);

        serverState.outQueue.shift();
        serverState.enabled = true;
        serverState.running = true;
        serverState.initializationInProgress = false;

        // todo: this might not work for all games
        responseEvent.server.rconParser.configuration.floodProtectInterval = 150;

        this.sendEventMessage(responseEvent.server, true, 'GetBusModeRequested', null, null, null, {});
        this.sendEventMessage(responseEvent.server, true, 'GetCommandsRequested', null, null, null, {});
        this.requestGetDvar(inDvar, responseEvent.server);
    },

    handleIncomingServerData: function (responseEvent) {
        this.logger.logDebug('[GameInterface] received {dvarName}={dvarValue} success={success} from {server}', responseEvent.response.name,
            responseEvent.response.value, responseEvent.success, responseEvent.server.id);

        const serverState = servers[responseEvent.server.id];
        // Server may have been removed - gracefully exit
        if (!serverState) {
            this.logger.logDebug('[GameInterface] server {serverId} was removed, ignoring incoming data', responseEvent.server.id);
            return;
        }
        serverState.outQueue.shift();

        const utilities = importNamespace('SharedLibraryCore.Utilities');

        if (responseEvent.server.connectedClients.count === 0 && !utilities.isDevelopment) {
            // no clients connected so we don't need to query
            this.logger.logDebug('[GameInterface] stopping game interface loop');
            serverState.running = false;
            return;
        }

        // read failed, so let's retry
        if (!responseEvent.success && !this.eventManager.cancellationToken.isCancellationRequested) {
            this.logger.logWarning('[GameInterface] get of server value failed... retrying');
            this.requestGetDvar(responseEvent.response.name, responseEvent.server);
            return;
        }

        let input = responseEvent.response.value;
        const server = responseEvent.server;

        if (this.eventManager.cancellationToken.isCancellationRequested) {
            this.logger.logDebug('[GameInterface] shutdown requested so we are ending loop');
            return;
        }

        // no data available so we poll again or send any outgoing messages
        if (isEmpty(input)) {
            this.logger.logDebug('[GameInterface] no data to process from server');
            if (serverState.commandQueue.length > 0) {
                this.logger.logDebug('[GameInterface] sending next out message');
                const nextMessage = serverState.commandQueue.shift();
                this.requestSetDvar(outDvar, nextMessage, server);
            } else {
                this.requestGetDvar(inDvar, server);
            }
            return;
        }

        serverState.inQueue.push(input);

        // let server know that we received the data
        this.requestSetDvar(inDvar, '', server);
    },

    processEventMessage: function (input, server) {
        let messageQueued = false;
        const event = parseEvent(input);

        this.logger.logDebug('[GameInterface] processing input... {eventType} {subType} {@data} {clientNumber}', event.eventType,
            event.subType, event.data, event.clientNumber);

        const metaService = this.serviceResolver.ResolveService('IMetaServiceV2');
        const threading = importNamespace('System.Threading');
        const tokenSource = new threading.CancellationTokenSource();
        const token = tokenSource.token;

        // todo: refactor to mapping if possible
        if (event.eventType === 'ClientDataRequested') {
            const client = server.getClientByNumber(event.clientNumber);

            if (client != null) {
                this.logger.logDebug('[GameInterface] Found client {name}', client.name);

                let data = [];

                const metaService = this.serviceResolver.resolveService('IMetaServiceV2');

                if (event.subType === 'Meta') {
                    const meta = (metaService.getPersistentMeta(event.data, client.clientId, token)).result;
                    data[event.data] = meta === null ? '' : meta.Value;
                    this.logger.logDebug('[GameInterface] event data is {data}', event.data);
                } else {
                    const clientStats = getClientStats(client, server);
                    const tagMeta = (metaService.getPersistentMetaByLookup('ClientTagV2', 'ClientTagNameV2', client.clientId, token)).result;
                    data = {
                        level: client.level,
                        clientId: client.clientId,
                        lastConnection: client.timeSinceLastConnectionString,
                        tag: tagMeta?.value ?? '',
                        performance: clientStats?.performance ?? 200.0,
                        ipAddress: client.ipAddressString
                    };
                }

                this.sendEventMessage(server, false, 'ClientDataReceived', event.subType, client, undefined, data);
                messageQueued = true;
            } else {
                this.logger.logWarning('[GameInterface] could not find client slot {clientNumber} when processing {eventType}', event.clientNumber, event.eventType);
                this.sendEventMessage(server, false, 'ClientDataReceived', 'Fail', event.clientNumber, undefined, {
                    ClientNumber: event.clientNumber
                });
                messageQueued = true;
            }
        }

        let _;
        if (event.eventType === 'SetClientDataRequested') {
            let client = server.getClientByNumber(event.clientNumber);
            let clientId;

            if (client != null) {
                clientId = client.clientId;
            } else {
                clientId = parseInt(event.data['clientId']);
            }

            this.logger.logDebug('[GameInterface] clientId={clientId}', clientId);

            if (clientId == null || isNaN(clientId)) {
                this.logger.logWarning('[GameInterface] could not find client slot {clientNumber} when processing {eventType}: {EventData}', event.clientNumber, event.eventType, event.data);
                this.sendEventMessage(server, false, 'SetClientDataCompleted', 'Meta', {
                    ClientNumber: event.clientNumber
                }, undefined, {
                    status: 'Fail'
                });
                messageQueued = true;
            } else {
                if (event.subType === 'Meta') {
                    try {
                        if (event.data['value'] != null && event.data['key'] != null) {
                            this.logger.logDebug('[GameInterface] key={key}, value={value}, direction={direction} {token}', event.data['key'], event.data['value'], event.data['direction'], token);
                            if (event.data['direction'] != null) {
                                const parsedValue = parseInt(event.data['value']);
                                const key = event.data['key'].toString();
                                if (!isNaN(parsedValue)) {
                                    _ = event.data['direction'] === 'increment' ?
                                        (metaService.incrementPersistentMeta(key, parsedValue, clientId, token)).result :
                                        (metaService.decrementPersistentMeta(key, parsedValue, clientId, token)).result;
                                }
                            } else {
                                _ = (metaService.setPersistentMeta(event.data['key'], event.data['value'], clientId, token)).result;
                            }

                            if (event.data['key'] === 'PersistentClientGuid') {
                                const serverEvents = importNamespace('SharedLibraryCore.Events.Management');
                                const persistentIdEvent = new serverEvents.ClientPersistentIdReceiveEvent(client, event.data['value']);
                                this.eventManager.queueEvent(persistentIdEvent);
                            }
                        }
                        this.sendEventMessage(server, false, 'SetClientDataCompleted', 'Meta', {
                            ClientNumber: event.clientNumber
                        }, undefined, {
                            status: 'Complete'
                        });
                        messageQueued = true;
                    } catch (error) {
                        this.sendEventMessage(server, false, 'SetClientDataCompleted', 'Meta', {
                            ClientNumber: event.clientNumber
                        }, undefined, {
                            status: 'Fail'
                        });
                        this.logger.logError('[GameInterface] could not persist client meta {Key}={Value} {error} for {Client}', event.data['key'], event.data['value'], error.toString(), clientId);
                        messageQueued = true;
                    }
                }
            }
        }

        if (event.eventType === 'UrlRequested') {
            const urlRequest = this.parseUrlRequest(event);

            this.logger.logDebug('[GameInterface] making gamescript web request {@Request}', urlRequest);

            this.scriptHelper.requestUrl(urlRequest, response => {
                this.logger.logDebug('[GameInterface] got response for gamescript web request - {Response}', response);

                if (typeof response !== 'string' && !(response instanceof String)) {
                    response = JSON.stringify(response);
                }

                const max = 10;
                this.logger.logDebug('[GameInterface] web url response length={length}', response.length);

                let quoteReplace = '\\"';
                // todo: may be more than just T6
                if (server.gameCode === 'T6') {
                    quoteReplace = '\\\\"';
                }

                let chunks = chunkString(response.replace(/"/gm, quoteReplace).replace(/[\n|\t]/gm, ''), 800);
                if (chunks.length > max) {
                    this.logger.logWarning('[GameInterface] response chunks greater than max ({max}). Data truncated!', max);
                    chunks = chunks.slice(0, max);
                }
                this.logger.logDebug('[GameInterface] response chunk size={Length}', chunks.length);

                for (let i = 0; i < chunks.length; i++) {
                    this.sendEventMessage(server, false, 'UrlRequestCompleted', null, null,
                        null, {entity: event.data.entity, remaining: chunks.length - (i + 1), response: chunks[i]});
                }
            });
        }

        if (event.eventType === 'RegisterCommandRequested') {
            this.registerDynamicCommand(event);
        }

        if (event.eventType === 'GetBusModeRequested') {
            if (event.data?.directory && event.data?.mode) {
                busMode = event.data.mode;
                busDir = event.data.directory.replace('\'', '').replace('"', '');
                if (event.data?.inLocation && event.data?.outLocation && busMode === 'file') {
                    busFileIn = event.data?.inLocation;
                    busFileOut = event.data?.outLocation;
                }
                this.logger.logDebug('[GameInterface] setting bus mode to {mode} dir={dir}', busMode, busDir);
            }
        }

        this.logger.logDebug('[GameInterface] finished processing input for {type}', event.eventType);

        tokenSource.dispose();
        return messageQueued;
    },

    sendEventMessage: function (server, responseExpected, event, subtype, origin, target, data) {
        if (!servers[server.id]) {
            this.logger.logDebug('[GameInterface] skipping sendEventMessage for removed server {id}', server.id);
            return;
        }

        let targetClientNumber = -1;
        let originClientNumber = -1;

        if (target != null) {
            targetClientNumber = target.clientNumber;
        }

        if (origin != null) {
            originClientNumber = origin.clientNumber
        }

        const output = `${responseExpected ? '1' : '0'}${groupSeparatorChar}${event}${groupSeparatorChar}${subtype}${groupSeparatorChar}${originClientNumber}${groupSeparatorChar}${targetClientNumber}${groupSeparatorChar}${buildDataString(data)}`;
        this.logger.logDebug('[GameInterface] queuing output for server {output}', output);

        servers[server.id].commandQueue.push(output);
    },

    requestGetDvar: function (dvarName, server) {
        const serverState = servers[server.id];

        if (!serverState) {
            this.logger.logDebug('[GameInterface] skipping requestGetDvar for removed server {id}', server.id);
            return;
        }

        if (dvarName !== integrationEnabledDvar && busMode === 'file') {
            this.scriptHelper.requestNotifyAfterDelay(250, () => {
                const io = importNamespace('System.IO');
                serverState.outQueue.push({});
                try {
                    const content = io.File.ReadAllText(`${busDir}/${fileForDvar(dvarName)}`);
                    plugin.onServerValueReceived({
                        server: server,
                        source: server,
                        success: true,
                        response: {
                            name: dvarName,
                            value: content
                        }
                    });
                } catch (e) {
                    plugin.logger.logError('[GameInterface] could not get bus data {exception}', e.toString());
                    plugin.onServerValueReceived({
                        server: server,
                        success: false,
                        response: {
                            name: dvarName
                        }
                    });
                }
            });

            return;
        }

        const serverEvents = importNamespace('SharedLibraryCore.Events.Server');
        const requestEvent = new serverEvents.ServerValueRequestEvent(dvarName, server);
        requestEvent.delayMs = this.config.pollingRate;
        requestEvent.timeoutMs = 2000;
        requestEvent.source = this.name;

        if (server.matchEndTime !== null) {
            const extraDelay = 15000;
            const end = new Date(server.matchEndTime.toString());
            const diff = new Date().getTime() - end.getTime();

            if (diff < extraDelay) {
                requestEvent.delayMs = (extraDelay - diff) + this.config.pollingRate;
                this.logger.logDebug('[GameInterface] increasing polling delay time to {Delay}ms due to recent map change', requestEvent.delayMs);
            }
        }

        this.logger.logDebug('[GameInterface] requesting {dvar}', dvarName);

        serverState.outQueue.push(requestEvent);

        if (serverState.outQueue.length <= 1) {
            this.eventManager.queueEvent(requestEvent);
        } else {
            this.logger.logError('[GameInterface::requestGetDvar] queue is full!');
        }
    },

    requestSetDvar: function (dvarName, dvarValue, server) {
        const serverState = servers[server.id];

        if (!serverState) {
            this.logger.logDebug('[GameInterface] skipping requestSetDvar for removed server {id}', server.id);
            return;
        }

        if (busMode === 'file') {
            this.scriptHelper.requestNotifyAfterDelay(250, () => {
                const io = importNamespace('System.IO');
                try {
                    const path = `${busDir}/${fileForDvar(dvarName)}`;
                    plugin.logger.logDebug('[GameInterface] writing {value} to {file}', dvarValue, path);
                    io.File.WriteAllText(path, dvarValue);
                    serverState.outQueue.push({});
                    plugin.onServerValueSetCompleted({
                        server: server,
                        source: server,
                        success: true,
                        value: dvarValue,
                        valueName: dvarName,
                    });
                } catch (e) {
                    plugin.logger.logError('[GameInterface] could not set bus data {exception}', e.toString());
                    plugin.onServerValueSetCompleted({
                        server: server,
                        success: false,
                        valueName: dvarName,
                        value: dvarValue
                    });
                }
            })

            return;
        }

        const serverEvents = importNamespace('SharedLibraryCore.Events.Server');
        const requestEvent = new serverEvents.ServerValueSetRequestEvent(dvarName, dvarValue, server);
        requestEvent.delayMs = this.config.pollingRate;
        requestEvent.timeoutMs = 2000;
        requestEvent.source = this.name;

        if (server.matchEndTime !== null) {
            const extraDelay = 15000;
            const end = new Date(server.matchEndTime.toString());
            const diff = new Date().getTime() - end.getTime();

            if (diff < extraDelay) {
                requestEvent.delayMs = (extraDelay - diff) + this.config.pollingRate;
                this.logger.logInformation('[GameInterface] increasing delay time to {Delay}ms due to recent map change', requestEvent.delayMs);
            }
        }

        serverState.outQueue.push(requestEvent);

        this.logger.logDebug('[GameInterface] outQueue size = {length}', serverState.outQueue.length);

        // if this is the only item in the out-queue we can send it immediately
        if (serverState.outQueue.length === 1) {
            this.eventManager.queueEvent(requestEvent);
        } else {
            this.logger.logError('[GameInterface::requestSetDvar] queue is full!');
        }
    },

    parseUrlRequest: function (event) {
        const url = event.data?.url;

        if (url === undefined) {
            this.logger.logWarning('[GameInterface] no url provided for gamescript web request - {@event}', event);
            return;
        }

        const body = event.data?.body;
        const method = event.data?.method || 'GET';
        const contentType = event.data?.contentType || 'text/plain';
        const headers = event.data?.headers;

        const dictionary = System.Collections.Generic.Dictionary(System.String, System.String);
        const headerDict = new dictionary();

        if (headers) {
            const eachHeader = headers.split(',');

            for (let eachKeyValue of eachHeader) {
                const keyValueSplit = eachKeyValue.split(':');
                if (keyValueSplit.length === 2) {
                    headerDict.add(keyValueSplit[0], keyValueSplit[1]);
                }
            }
        }

        const script = importNamespace('IW4MAdmin.Application.Plugin.Script');
        return new script.ScriptPluginWebRequest(url, body, method, contentType, headerDict);
    },

    registerDynamicCommand: function (event) {
        const commandWrapper = {
            commands: [{
                name: event.data['name'] || 'DEFAULT',
                description: event.data['description'] || 'DEFAULT',
                alias: event.data['alias'] || 'DEFAULT',
                permission: event.data['minPermission'] || 'DEFAULT',
                targetRequired: (event.data['targetRequired'] || '0') === '1',
                supportedGames: (event.data['supportedGames'] || '').split(','),

                execute: (gameEvent) => {
                    if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                        return;
                    }

                    if (gameEvent.data === '--reload' && gameEvent.origin.level === 'Owner') {
                        this.sendEventMessage(gameEvent.owner, true, 'GetCommandsRequested', null, null, null, {name: gameEvent.extra.name});
                    } else {
                        sendScriptCommand(gameEvent.owner, `${event.data['eventKey']}Execute`, gameEvent.origin, gameEvent.target, {
                            args: gameEvent.data
                        });
                    }
                }
            }]
        }

        this.scriptHelper.registerDynamicCommand(commandWrapper);
    }
};

const commands = [{
    name: 'giveweapon',
    description: 'gives specified weapon',
    alias: 'gw',
    permission: 'SeniorAdmin',
    targetRequired: true,
    arguments: [{
        name: 'player',
        required: true
    },
        {
            name: 'weapon name',
            required: true
        }
    ],
    supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
    execute: (gameEvent) => {
        if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
            return;
        }
        sendScriptCommand(gameEvent.owner, 'GiveWeapon', gameEvent.origin, gameEvent.target, {
            weaponName: gameEvent.data
        });
    }
},
    {
        name: 'takeweapons',
        description: 'take all weapons from specified player',
        alias: 'tw',
        permission: 'SeniorAdmin',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'TakeWeapons', gameEvent.origin, gameEvent.target, undefined);
        }
    },
    {
        name: 'switchteam',
        description: 'switches specified player to the opposite team',
        alias: 'st',
        permission: 'Administrator',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'SwitchTeams', gameEvent.origin, gameEvent.target, undefined);
        }
    },
    {
        name: 'lockcontrols',
        description: 'locks target player\'s controls',
        alias: 'lc',
        permission: 'Administrator',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'LockControls', gameEvent.origin, gameEvent.target, undefined);
        }
    },
    {
        name: 'noclip',
        description: 'enable noclip on yourself ingame',
        alias: 'nc',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'NoClip', gameEvent.origin, gameEvent.origin, undefined);
        }
    },
    {
        name: 'hide',
        description: 'hide yourself ingame',
        alias: 'hi',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'Hide', gameEvent.origin, gameEvent.origin, undefined);
        }
    },
    {
        name: 'alert',
        description: 'alert a player',
        alias: 'alr',
        permission: 'SeniorAdmin',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        },
            {
                name: 'message',
                required: true
            }
        ],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'Alert', gameEvent.origin, gameEvent.target, {
                alertType: 'Alert',
                message: gameEvent.Data
            });
        }
    },
    {
        name: 'gotoplayer',
        description: 'teleport to a player',
        alias: 'g2p',
        permission: 'SeniorAdmin',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'Goto', gameEvent.origin, gameEvent.target, undefined);
        }
    },
    {
        name: 'playertome',
        description: 'teleport a player to you',
        alias: 'p2m',
        permission: 'SeniorAdmin',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'PlayerToMe', gameEvent.origin, gameEvent.target, undefined);
        }
    },
    {
        name: 'goto',
        description: 'teleport to a position',
        alias: 'g2',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [{
            name: 'x',
            required: true
        },
            {
                name: 'y',
                required: true
            },
            {
                name: 'z',
                required: true
            }
        ],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }

            const args = String(gameEvent.Data).split(' ');
            sendScriptCommand(gameEvent.owner, 'Goto', gameEvent.origin, gameEvent.target, {
                x: args[0],
                y: args[1],
                z: args[2]
            });
        }
    },
    {
        name: 'kill',
        description: 'kill a player',
        alias: 'kpl',
        permission: 'SeniorAdmin',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'Kill', gameEvent.origin, gameEvent.target, undefined);
        }
    },
    {
        name: 'setspectator',
        description: 'sets a player as spectator',
        alias: 'spec',
        permission: 'Administrator',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5', 'T5', 'T6'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.owner, gameEvent.origin)) {
                return;
            }
            sendScriptCommand(gameEvent.owner, 'SetSpectator', gameEvent.origin, gameEvent.target, undefined);
        }
    }
];

const sendScriptCommand = (server, command, origin, target, data) => {
    const serverState = servers[server.id];
    if (serverState === undefined || !serverState.enabled) {
        return;
    }
    plugin.sendEventMessage(server, false, 'ExecuteCommandRequested', command, origin, target, data);
};

const getClientStats = (client, server) => {
    const contextFactory = plugin.serviceResolver.ResolveService('IDatabaseContextFactory');
    const context = contextFactory.createContext(false);
    const stats = context.clientStatistics.getClientsStatData([client.ClientId], server.legacyDatabaseId);
    context.dispose();

    return stats.length > 0 ? stats[0] : undefined;
};

const parseEvent = (input) => {
    if (input === undefined) {
        return {};
    }

    const eventInfo = input.split(groupSeparatorChar);

    return {
        eventType: eventInfo[1],
        subType: eventInfo[2],
        clientNumber: eventInfo[3],
        data: eventInfo.length > 4 ? parseDataString(eventInfo[4]) : undefined
    };
};

const buildDataString = data => {
    if (data === undefined) {
        return '';
    }

    let formattedData = '';

    for (let [key, value] of Object.entries(data)) {
        formattedData += `${key}${unitSeparatorChar}${value}${recordSeparatorChar}`;
    }

    return formattedData.slice(0, -1);
};

const parseDataString = data => {
    if (data === undefined) {
        return '';
    }

    const dict = {};
    const split = data.split(recordSeparatorChar);

    for (let i = 0; i < split.length; i++) {
        const segment = split[i];
        const keyValue = segment.split(unitSeparatorChar);
        if (keyValue.length !== 2) {
            continue;
        }
        dict[keyValue[0]] = keyValue[1];
    }

    return Object.keys(dict).length === 0 ? data : dict;
};

const validateEnabled = (server, origin) => {
    const enabled = servers[server.id] != null && servers[server.id].enabled;
    if (!enabled) {
        origin.tell('Game interface is not enabled on this server');
    }
    return enabled;
};

const isEmpty = (value) => {
    return value == null || false || value === '' || value === 'null';
};

const chunkString = (str, chunkSize) => {
    const result = [];
    for (let i = 0; i < str.length; i += chunkSize) {
        result.push(str.slice(i, i + chunkSize));
    }

    return result;
}

const fileForDvar = (dvar) => {
    if (dvar === inDvar) {
        return busFileIn;
    }

    return busFileOut;
}
