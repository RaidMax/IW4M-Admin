const servers = {};
const inDvar = 'sv_iw4madmin_in';
const outDvar = 'sv_iw4madmin_out';
const integrationEnabledDvar = 'sv_iw4madmin_integration_enabled';
const pollingRate = 300;

const init = (registerNotify, serviceResolver, config) => {
    registerNotify('IManagementEventSubscriptions.ClientStateInitialized', (clientEvent, _) => plugin.onClientEnteredMatch(clientEvent));
    registerNotify('IGameServerEventSubscriptions.ServerValueReceived', (serverValueEvent, _) => plugin.onServerValueReceived(serverValueEvent));
    registerNotify('IGameServerEventSubscriptions.ServerValueSetCompleted', (serverValueEvent, _) => plugin.onServerValueSetCompleted(serverValueEvent));
    registerNotify('IGameServerEventSubscriptions.MonitoringStarted', (monitorStartEvent, _) => plugin.onServerMonitoringStart(monitorStartEvent));
    registerNotify('IManagementEventSubscriptions.ClientPenaltyAdministered', (penaltyEvent, _) => plugin.onPenalty(penaltyEvent));

    plugin.onLoad(serviceResolver, config);
    return plugin;
};

const plugin = {
    author: 'RaidMax',
    version: '2.0',
    name: 'Game Interface',
    serviceResolver: null,
    eventManager: null,
    logger: null,
    commands: null,

    onLoad: function (serviceResolver, config) {
        this.serviceResolver = serviceResolver;
        this.eventManager = serviceResolver.resolveService('IManager');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.commands = commands;
        this.config = config;
    },

    onClientEnteredMatch: function (clientEvent) {
        const serverState = servers[clientEvent.client.currentServer.id];

        if (serverState === undefined || serverState == null) {
            this.initializeServer(clientEvent.client.currentServer);
        } else if (!serverState.running && !serverState.initializationInProgress) {
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

    onServerValueSetCompleted: async function (serverValueEvent) {
        if (serverValueEvent.valueName !== inDvar && serverValueEvent.valueName !== outDvar) {
            this.logger.logDebug('Ignoring set complete of {name}', serverValueEvent.valueName);
            return;
        }

        const serverState = servers[serverValueEvent.server.id];
        serverState.outQueue.shift();

        this.logger.logDebug('outQueue len = {outLen}, inQueue len = {inLen}', serverState.outQueue.length, serverState.inQueue.length);

        // if it didn't succeed, we need to retry
        if (!serverValueEvent.success && !this.eventManager.cancellationToken.isCancellationRequested) {
            this.logger.logDebug('Set of server value failed... retrying');
            this.requestSetDvar(serverValueEvent.valueName, serverValueEvent.value, serverValueEvent.server);
            return;
        }

        // we informed the server that we received the event
        if (serverState.inQueue.length > 0 && serverValueEvent.valueName === inDvar) {
            const input = serverState.inQueue.shift();

            // if we queued an event then the next loop will be at the value set complete
            if (await this.processEventMessage(input, serverValueEvent.server)) {
                // return;
            }
        }

        this.logger.logDebug('loop complete');
        // loop restarts
        this.requestGetDvar(inDvar, serverValueEvent.server);
    },

    initializeServer: function (server) {
        servers[server.id] = {
            enabled: false,
            running: false,
            initializationInProgress: true,
            queuedMessages: [],
            inQueue: [],
            outQueue: [],
            commandQueue: []
        };

        this.logger.logDebug('Initializing game interface for {serverId}', server.id);
        this.requestGetDvar(integrationEnabledDvar, server);
    },

    handleInitializeServerData: function (responseEvent) {
        this.logger.logInformation('GSC integration enabled = {integrationValue} for {server}',
            responseEvent.response.value, responseEvent.server.id);

        if (responseEvent.response.value !== '1') {
            return;
        }

        const serverState = servers[responseEvent.server.id];
        serverState.outQueue.shift();
        serverState.enabled = true;
        serverState.running = true;
        serverState.initializationInProgress = false;

        this.requestGetDvar(inDvar, responseEvent.server);
    },

    handleIncomingServerData: function (responseEvent) {
        this.logger.logDebug('Received {dvarName}={dvarValue} success={success} from {server}', responseEvent.response.name,
            responseEvent.response.value, responseEvent.success, responseEvent.server.id);

        const serverState = servers[responseEvent.server.id];
        serverState.outQueue.shift();

        if (responseEvent.server.connectedClients.count === 0) {
            // no clients connected so we don't need to query
            serverState.running = false;
            return;
        }

        // read failed, so let's retry
        if (!responseEvent.success && !this.eventManager.cancellationToken.isCancellationRequested) {
            this.logger.logDebug('Get of server value failed... retrying');
            this.requestGetDvar(responseEvent.response.name, responseEvent.server);
            return;
        }

        let input = responseEvent.response.value;
        const server = responseEvent.server;

        if (this.eventManager.cancellationToken.isCancellationRequested) {
            return;
        }

        // no data available so we poll again or send any outgoing messages
        if (isEmpty(input)) {
            this.logger.logDebug('No data to process from server');
            if (serverState.commandQueue.length > 0) {
                this.logger.logDebug('Sending next out message');
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

    processEventMessage: async function (input, server) {
        let messageQueued = false;
        const event = parseEvent(input);

        this.logger.logDebug('Processing input... {eventType} {subType} {data} {clientNumber}', event.eventType,
            event.subType, event.data.toString(), event.clientNumber);

        const metaService = this.serviceResolver.ResolveService('IMetaServiceV2');
        const threading = importNamespace('System.Threading');
        const tokenSource = new threading.CancellationTokenSource();
        const token = tokenSource.token;

        // todo: refactor to mapping if possible
        if (event.eventType === 'ClientDataRequested') {
            const client = server.getClientByNumber(event.clientNumber);

            if (client != null) {
                this.logger.logDebug('Found client {name}', client.name);

                let data = [];

                const metaService = this.serviceResolver.resolveService('IMetaServiceV2');

                if (event.subType === 'Meta') {
                    const meta = (await metaService.getPersistentMeta(event.data, client.clientId, token)).result;
                    data[event.data] = meta === null ? '' : meta.Value;
                    this.logger.logDebug('event data is {data}', event.data);
                } else {
                    const clientStats = getClientStats(client, server);
                    const tagMeta = (await metaService.getPersistentMetaByLookup('ClientTagV2', 'ClientTagNameV2', client.clientId, token)).result;
                    data = {
                        level: client.level,
                        clientId: client.clientId,
                        lastConnection: client.lastConnection,
                        tag: tagMeta?.value ?? '',
                        performance: clientStats?.performance ?? 200.0
                    };
                }

                this.sendEventMessage(server, false, 'ClientDataReceived', event.subType, client, undefined, data);
                messageQueued = true;
            } else {
                this.logger.logWarning('Could not find client slot {clientNumber} when processing {eventType}', event.clientNumber, event.eventType);
                this.sendEventMessage(server, false, 'ClientDataReceived', 'Fail', event.clientNumber, undefined, {
                    ClientNumber: event.clientNumber
                });
                messageQueued = true;
            }
        }

        if (event.eventType === 'SetClientDataRequested') {
            let client = server.getClientByNumber(event.clientNumber);
            let clientId;

            if (client != null) {
                clientId = client.clientId;
            } else {
                clientId = parseInt(event.data['clientId']);
            }

            this.logger.logDebug('ClientId={clientId}', clientId);

            if (clientId == null || isNaN(clientId)) {
                this.logger.logWarning('Could not find client slot {clientNumber} when processing {eventType}: {EventData}', event.clientNumber, event.eventType, event.data);
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
                            this.logger.logDebug('Key={key}, Value={value}, Direction={direction} {token}', event.data['key'], event.data['value'], event.data['direction'], token);
                            if (event.data['direction'] != null) {
                                const parsedValue = parseInt(event.data['value']);
                                const key = event.data['key'].toString();
                                if (!isNaN(parsedValue)) {
                                    event.data['direction'] = 'up' ?
                                        (await metaService.incrementPersistentMeta(key, parsedValue, clientId, token)).result :
                                        (await metaService.decrementPersistentMeta(key, parsedValue, clientId, token)).result;
                                }
                            } else {
                                const _ = (await metaService.setPersistentMeta(event.data['key'], event.data['value'], clientId, token)).result;
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
                        this.logger.logError('Could not persist client meta {Key}={Value} {error} for {Client}', event.data['key'], event.data['value'], error.toString(), clientId);
                        messageQueued = true;
                    }
                }
            }
        }

        tokenSource.dispose();
        return messageQueued;
    },

    sendEventMessage: function (server, responseExpected, event, subtype, origin, target, data) {
        let targetClientNumber = -1;
        if (target != null) {
            targetClientNumber = target.ClientNumber;
        }

        const output = `${responseExpected ? '1' : '0'};${event};${subtype};${origin.ClientNumber};${targetClientNumber};${buildDataString(data)}`;
        this.logger.logDebug('Queuing output for server {output}', output);

        servers[server.id].commandQueue.push(output);
    },

    requestGetDvar: function (dvarName, server) {
        const serverState = servers[server.id];
        const serverEvents = importNamespace('SharedLibraryCore.Events.Server');
        const requestEvent = new serverEvents.ServerValueRequestEvent(dvarName, server);
        requestEvent.delayMs = pollingRate;
        requestEvent.timeoutMs = 2000;
        requestEvent.source = this.name;

        if (server.matchEndTime !== null) {
            const extraDelay = 15000;
            const end = new Date(server.matchEndTime.toString());
            const diff = new Date().getTime() - end.getTime();

            if (diff < extraDelay) {
                requestEvent.delayMs = (extraDelay - diff) + pollingRate;
                this.logger.logDebug('Increasing delay time to {Delay}ms due to recent map change', requestEvent.delayMs);
            }
        }

        this.logger.logDebug('requesting {dvar}', dvarName);

        serverState.outQueue.push(requestEvent);

        if (serverState.outQueue.length <= 1) {
            this.eventManager.queueEvent(requestEvent);
        } else {
            this.logger.logError('[requestGetDvar] Queue is full!');
        }
    },

    requestSetDvar: function (dvarName, dvarValue, server) {
        const serverState = servers[server.id];

        const serverEvents = importNamespace('SharedLibraryCore.Events.Server');
        const requestEvent = new serverEvents.ServerValueSetRequestEvent(dvarName, dvarValue, server);
        requestEvent.delayMs = pollingRate;
        requestEvent.timeoutMs = 2000;
        requestEvent.source = this.name;

        if (server.matchEndTime !== null) {
            const extraDelay = 15000;
            const end = new Date(server.matchEndTime.toString());
            const diff = new Date().getTime() - end.getTime();

            if (diff < extraDelay) {
                requestEvent.delayMs = (extraDelay - diff) + pollingRate;
                this.logger.logDebug('Increasing delay time to {Delay}ms due to recent map change', requestEvent.delayMs);
            }
        }

        serverState.outQueue.push(requestEvent);

        this.logger.logDebug('outQueue size = {length}', serverState.outQueue.length);

        // if this is the only item in the out-queue we can send it immediately
        if (serverState.outQueue.length === 1) {
            this.eventManager.queueEvent(requestEvent);
        } else {
            this.logger.logError('[requestSetDvar] Queue is full!');
        }
    },

    onServerMonitoringStart: function (monitorStartEvent) {
        this.initializeServer(monitorStartEvent.server);
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
    supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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
        supportedGames: ['IW4', 'IW5', 'T5'],
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

    const eventInfo = input.split(';');

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
        formattedData += `${key}=${value}|`;
    }

    return formattedData.slice(0, -1);
};

const parseDataString = data => {
    if (data === undefined) {
        return '';
    }

    const dict = {};
    const split = data.split('|');

    for (let i = 0; i < split.length; i++) {
        const segment = split[i];
        const keyValue = segment.split('=');
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
