const servers = {};
const inDvar = 'sv_iw4madmin_in';
const outDvar = 'sv_iw4madmin_out';
const pollRate = 900;
const enableCheckTimeout = 10000;
let logger = {};
const maxQueuedMessages = 25;

let plugin = {
    author: 'RaidMax',
    version: 1.1,
    name: 'Game Interface',

    onEventAsync: (gameEvent, server) => {
        if (servers[server.EndPoint] != null && !servers[server.EndPoint].enabled) {
            return;
        }

        const eventType = String(gameEvent.TypeName).toLowerCase();

        if (eventType === undefined) {
            return;
        }

        switch (eventType) {
            case 'start':
                const enabled = initialize(server);

                if (!enabled) {
                    return;
                }
                break;
            case 'preconnect':
                // when the plugin is reloaded after the servers are started
                if (servers[server.EndPoint] === undefined || servers[server.EndPoint] == null) {
                    const enabled = initialize(server);

                    if (!enabled) {
                        return;
                    }
                }
                const timer = servers[server.EndPoint].timer;
                if (!timer.IsRunning) {
                    timer.Start(0, pollRate);
                }
                break;
            case 'warn':
                const warningTitle = _localization.LocalizationIndex['GLOBAL_WARNING'];
                sendScriptCommand(server, 'Alert', gameEvent.Origin, gameEvent.Target, {
                    alertType: warningTitle + '!',
                    message: gameEvent.Data
                });
                break;
        }
    },

    onLoadAsync: manager => {
        logger = _serviceResolver.ResolveService('ILogger');
        logger.WriteInfo('Game Interface Startup');
    },

    onUnloadAsync: () => {
        for (let i = 0; i < servers.length; i++) {
            if (servers[i].enabled) {
                servers[i].timer.Stop();
            }
        }
    },

    onTickAsync: server => {
    }
};

let commands = [{
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
        }],
    supportedGames: ['IW4', 'IW5'],
    execute: (gameEvent) => {
        if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
            return;
        }
        sendScriptCommand(gameEvent.Owner, 'GiveWeapon', gameEvent.Origin, gameEvent.Target, {weaponName: gameEvent.Data});
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'TakeWeapons', gameEvent.Origin, gameEvent.Target, undefined);
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'SwitchTeams', gameEvent.Origin, gameEvent.Target, undefined);
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'LockControls', gameEvent.Origin, gameEvent.Target, undefined);
        }
    },
    {
        name: 'unlockcontrols',
        description: 'unlocks target player\'s controls',
        alias: 'ulc',
        permission: 'Administrator',
        targetRequired: true,
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'UnlockControls', gameEvent.Origin, gameEvent.Target, undefined);
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
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'NoClip', gameEvent.Origin, gameEvent.Origin, undefined);
        }
    },
    {
        name: 'noclipoff',
        description: 'disable noclip on yourself ingame',
        alias: 'nco',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'NoClipOff', gameEvent.Origin, gameEvent.Origin, undefined);
        }
    },
    {
        name: 'hide',
        description: 'hide yourself ingame',
        alias: 'hi',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'Hide', gameEvent.Origin, gameEvent.Origin, undefined);
        }
    },
    {
        name: 'unhide',
        description: 'unhide yourself ingame',
        alias: 'unh',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'Unhide', gameEvent.Origin, gameEvent.Origin, undefined);
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
            }],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'Alert', gameEvent.Origin, gameEvent.Target, {
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'Goto', gameEvent.Origin, gameEvent.Target, undefined);
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'PlayerToMe', gameEvent.Origin, gameEvent.Target, undefined);
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
            }],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }

            const args = String(gameEvent.Data).split(' ');
            sendScriptCommand(gameEvent.Owner, 'Goto', gameEvent.Origin, gameEvent.Target, {
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'Kill', gameEvent.Origin, gameEvent.Target, undefined);
        }
    },
    {
        name: 'nightmode',
        description: 'sets server into nightmode',
        alias: 'nitem',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [],
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'NightMode', gameEvent.Origin, undefined, undefined);
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
        supportedGames: ['IW4', 'IW5'],
        execute: (gameEvent) => {
            if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
                return;
            }
            sendScriptCommand(gameEvent.Owner, 'SetSpectator', gameEvent.Origin, gameEvent.Target, undefined);
        }
    }];

const sendScriptCommand = (server, command, origin, target, data) => {
    const state = servers[server.EndPoint];
    if (state === undefined || !state.enabled) {
        return;
    }
    sendEvent(server, false, 'ExecuteCommandRequested', command, origin, target, data);
}

const sendEvent = (server, responseExpected, event, subtype, origin, target, data) => {
    const logger = _serviceResolver.ResolveService('ILogger');
    const state = servers[server.EndPoint];

    if (state.queuedMessages.length >= maxQueuedMessages) {
        logger.WriteWarning('Too many queued messages so we are skipping');
        return;
    }

    let targetClientNumber = -1;
    if (target != null) {
        targetClientNumber = target.ClientNumber;
    }

    const output = `${responseExpected ? '1' : '0'};${event};${subtype};${origin.ClientNumber};${targetClientNumber};${buildDataString(data)}`;
    logger.WriteDebug(`Queuing output for server ${output}`);

    state.queuedMessages.push(output);
};

const initialize = (server) => {
    const logger = _serviceResolver.ResolveService('ILogger');

    servers[server.EndPoint] = {
        enabled: false
    }

    let enabled = false;
    try {
        enabled = server.GetServerDvar('sv_iw4madmin_integration_enabled', enableCheckTimeout) === '1';
    } catch (error) {
        logger.WriteError(`Could not get integration status of ${server.EndPoint} - ${error}`);
    }

    logger.WriteInfo(`GSC Integration enabled = ${enabled}`);

    if (!enabled) {
        return false;
    }

    logger.WriteDebug(`Setting up bus timer for ${server.EndPoint}`);

    let timer = _serviceResolver.ResolveService('IScriptPluginTimerHelper');
    timer.OnTick(() => pollForEvents(server), `GameEventPoller ${server.ToString()}`);
    // necessary to prevent multi-threaded access to the JS context
    timer.SetDependency(_lock);

    servers[server.EndPoint].timer = timer;
    servers[server.EndPoint].enabled = true;
    servers[server.EndPoint].waitingOnInput = false;
    servers[server.EndPoint].waitingOnOutput = false;
    servers[server.EndPoint].queuedMessages = [];

    setDvar(server, inDvar, '', onSetDvar);
    setDvar(server, outDvar, '', onSetDvar);

    return true;
}

function onReceivedDvar(server, dvarName, dvarValue, success) {
    const logger = _serviceResolver.ResolveService('ILogger');
    logger.WriteDebug(`Received ${dvarName}=${dvarValue} success=${success}`);

    let input = dvarValue;
    const state = servers[server.EndPoint];

    if (state.waitingOnOutput && dvarName === outDvar && isEmpty(dvarValue)) {
        logger.WriteDebug('Setting out bus to read to send');
        // reset our flag letting use the out bus is open
        state.waitingOnOutput = !success;
    }

    if (state.waitingOnInput && dvarName === inDvar) {
        logger.WriteDebug('Setting in bus to ready to receive');
        // we've received the data so now we can mark it as ready for more
        state.waitingOnInput = false;
    }

    if (isEmpty(input)) {
        input = '';
    }

    if (input.length > 0) {
        const event = parseEvent(input)

        logger.WriteDebug(`Processing input... ${event.eventType} ${event.subType} ${event.data.toString()} ${event.clientNumber}`);

        const metaService = _serviceResolver.ResolveService('IMetaServiceV2');
        const threading = importNamespace('System.Threading');
        const token = new threading.CancellationTokenSource().Token;

        // todo: refactor to mapping if possible
        if (event.eventType === 'ClientDataRequested') {
            const client = server.GetClientByNumber(event.clientNumber);

            if (client != null) {
                logger.WriteDebug(`Found client ${client.Name}`);

                let data = [];

                if (event.subType === 'Meta') {
                    const metaService = _serviceResolver.ResolveService('IMetaServiceV2');
                    const meta = metaService.GetPersistentMeta(event.data, client, token).GetAwaiter().GetResult();
                    data[event.data] = meta === null ? '' : meta.Value;
                } else {
                    data = {
                        level: client.Level,
                        clientId: client.ClientId,
                        lastConnection: client.LastConnection
                    };
                }

                sendEvent(server, false, 'ClientDataReceived', event.subType, client, undefined, data);
            } else {
                logger.WriteWarning(`Could not find client slot ${event.clientNumber} when processing ${event.eventType}`);
                sendEvent(server, false, 'ClientDataReceived', 'Fail', event.clientNumber, undefined, {ClientNumber: event.clientNumber});
            }
        }

        if (event.eventType === 'SetClientDataRequested') {
            let client = server.GetClientByNumber(event.clientNumber);
            let clientId;

            if (client != null) {
                clientId = client.ClientId;
            } else {
                clientId = parseInt(event.data.clientId);
            }

            logger.WriteDebug(`ClientId=${clientId}`);

            if (clientId == null) {
                logger.WriteWarning(`Could not find client slot ${event.clientNumber} when processing ${event.eventType}`);
                sendEvent(server, false, 'SetClientDataCompleted', 'Meta', {ClientNumber: event.clientNumber}, undefined, {status: 'Fail'});
            } else {
                if (event.subType === 'Meta') {
                    try {
                        logger.WriteDebug(`Key=${event.data['key']}, Value=${event.data['value']}, Direction=${event.data['direction']} ${token}`);
                        if (event.data['direction'] != null) {
                            event.data['direction'] = 'up'
                                ? metaService.IncrementPersistentMeta(event.data['key'], event.data['value'], clientId, token).GetAwaiter().GetResult()
                                : metaService.DecrementPersistentMeta(event.data['key'], event.data['value'], clientId, token).GetAwaiter().GetResult();
                        } else {
                            metaService.SetPersistentMeta(event.data['key'], event.data['value'], clientId, token).GetAwaiter().GetResult();
                        }
                        sendEvent(server, false, 'SetClientDataCompleted', 'Meta', {ClientNumber: event.clientNumber}, undefined, {status: 'Complete'});
                    } catch (error) {
                        sendEvent(server, false, 'SetClientDataCompleted', 'Meta', {ClientNumber: event.clientNumber}, undefined, {status: 'Fail'});
                        logger.WriteError('Could not persist client meta ' + error.toString());
                    }
                }
            }
        }

        setDvar(server, inDvar, '', onSetDvar);
    } else if (server.ClientNum === 0) {
        servers[server.EndPoint].timer.Stop();
    }
}

function onSetDvar(server, dvarName, dvarValue, success) {
    const logger = _serviceResolver.ResolveService('ILogger');
    logger.WriteDebug(`Completed set of dvar ${dvarName}=${dvarValue}, success=${success}`);

    const state = servers[server.EndPoint];

    if (dvarName === inDvar && success && isEmpty(dvarValue)) {
        logger.WriteDebug('In bus is ready for new data');
        // reset our flag letting use the in bus is ready for more data
        state.waitingOnInput = false;
    }
}

const pollForEvents = server => {
    const state = servers[server.EndPoint];

    if (state === null || !state.enabled) {
        return;
    }

    if (server.Throttled) {
        return;
    }

    if (!state.waitingOnInput) {
        state.waitingOnInput = true;
        getDvar(server, inDvar, onReceivedDvar);
    }

    if (!state.waitingOnOutput) {
        if (state.queuedMessages.length === 0) {
            logger.WriteDebug('No messages in queue');
            return;
        }

        state.waitingOnOutput = true;
        const nextMessage = state.queuedMessages.splice(0, 1);
        setDvar(server, outDvar, nextMessage, onSetDvar);
    }

    if (state.waitingOnOutput) {
        getDvar(server, outDvar, onReceivedDvar);
    }
}

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
    }
}

const buildDataString = data => {
    if (data === undefined) {
        return '';
    }

    let formattedData = '';

    for (const prop in data) {
        formattedData += `${prop}=${data[prop]}|`;
    }

    return formattedData.substring(0, Math.max(0, formattedData.length - 1));
}

const parseDataString = data => {
    if (data === undefined) {
        return '';
    }

    const dict = {}

    for (const segment of data.split('|')) {
        const keyValue = segment.split('=');
        if (keyValue.length !== 2) {
            continue;
        }
        dict[keyValue[0]] = keyValue[1];
    }

    return dict.length === 0 ? data : dict;
}

const validateEnabled = (server, origin) => {
    const enabled = servers[server.EndPoint] != null && servers[server.EndPoint].enabled;
    if (!enabled) {
        origin.Tell('Game interface is not enabled on this server');
    }
    return enabled;
}

function isEmpty(value) {
    return value == null || false || value === '' || value === 'null';
}
