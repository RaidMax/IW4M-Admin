const eventTypes = {
    1: 'start', // a server started being monitored
    6: 'disconnect', // a client detected a leaving the game
    9: 'preconnect', // client detected as joining via log or status
    101: 'warn' // client was warned
};

const servers = {};
const inDvar = 'sv_iw4madmin_in';
const outDvar = 'sv_iw4madmin_out';
const pollRate = 750;
let logger = {};

let plugin = {
    author: 'RaidMax',
    version: 1.0,
    name: 'Game Interface',

    onEventAsync: (gameEvent, server) => {
        if (servers[server.EndPoint] != null && !servers[server.EndPoint].enabled) {
            return;
        }

        const eventType = eventTypes[gameEvent.Type];

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
                sendScriptCommand(server, 'Alert', gameEvent.Target, {
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
    execute: (gameEvent) => {
        if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
            return;
        }
        sendScriptCommand(gameEvent.Owner, 'SwitchTeams', gameEvent.Origin, gameEvent.Target, undefined);
    }
},
{
    name: 'hide',
    description: 'hide yourself ingame',
    alias: 'hi',
    permission: 'SeniorAdmin',
    targetRequired: false,
    arguments: [],
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
    execute: (gameEvent) => {
        if (!validateEnabled(gameEvent.Owner, gameEvent.Origin)) {
            return;
        }
        sendScriptCommand(gameEvent.Owner, 'Goto', gameEvent.Origin, gameEvent.Target, undefined);
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
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
    supportedGames: ['IW4'],
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

    let pendingOut = true;
    let pendingCheckCount = 0;
    const start = new Date();

    while (pendingOut && pendingCheckCount <= 10) {
        if (server.Throttled) {
            logger.WriteWarning('Server is throttled, so we are not attempting to send data');
            return;
        }
        
        try {
            const out = server.GetServerDvar(outDvar);
            pendingOut = !(out == null || out === '' || out === 'null');
        } catch (error) {
            logger.WriteError(`Could not check server output dvar for IO status ${error}`);
        }

        if (pendingOut) {
            logger.WriteDebug('Waiting for event bus to be cleared');
            System.Threading.Tasks.Task.Delay(1000).Wait();
        }
        
        pendingCheckCount++;
    }

    if (pendingOut) {
        logger.WriteWarning(`Reached maximum attempts waiting for output to be available for ${server.EndPoint}`)
    }

    let targetClientNumber = -1;
    if (target != null) {
        targetClientNumber = target.ClientNumber;
    }

    const output = `${responseExpected ? '1' : '0'};${event};${subtype};${origin.ClientNumber};${targetClientNumber};${buildDataString(data)}`;
    logger.WriteDebug(`Sending output to server ${output}`);

    try {
        server.SetServerDvar(outDvar, output);
        logger.WriteDebug(`SendEvent took ${(new Date() - start) / 1000}ms`);
    } catch (error) {
        logger.WriteError(`Could not set server output dvar ${error}`);
    }
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
    }
}

const initialize = (server) => {
    const logger = _serviceResolver.ResolveService('ILogger');

    servers[server.EndPoint] = {
        enabled: false
    }

    let enabled = false;
    try {
        enabled = server.GetServerDvar('sv_iw4madmin_integration_enabled') === '1';
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

    try {
        server.SetServerDvar(inDvar, '');
        server.SetServerDvar(outDvar, '');
    } catch (error) {
        logger.WriteError(`Could set default values bus dvars for ${server.EndPoint} - ${error}`);
    }

    return true;
};

const pollForEvents = server => {
    if (server.Throttled) {
        return;
    }
    
    const logger = _serviceResolver.ResolveService('ILogger');

    let input;
    try {
        input = server.GetServerDvar(inDvar);
    } catch (error) {
        logger.WriteError(`Could not get input bus value for ${server.EndPoint} - ${error}`);
        return;
    }

    if (input === undefined || input === null || input === 'null') {
        input = '';
    }

    if (input.length > 0) {
        const event = parseEvent(input)

        logger.WriteDebug(`Processing input... ${event.eventType} ${event.subType} ${event.data} ${event.clientNumber}`);

        // todo: refactor to mapping if possible
        if (event.eventType === 'ClientDataRequested') {
            const client = server.GetClientByNumber(event.clientNumber);

            if (client != null) {
                logger.WriteDebug(`Found client ${client.Name}`);

                let data = [];

                if (event.subType === 'Meta') {
                    const metaService = _serviceResolver.ResolveService('IMetaService');
                    const meta = metaService.GetPersistentMeta(event.data, client).GetAwaiter().GetResult();
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
                    const metaService = _serviceResolver.ResolveService('IMetaService');
                    try {
                        logger.WriteDebug(`Key=${event.data['key']}, Value=${event.data['value']}`);
                        if (event.data['direction'] != null) {
                            event.data['direction'] = 'up'
                                ? metaService.IncrementPersistentMeta(event.data['key'], event.data['value'], clientId).GetAwaiter().GetResult()
                                : metaService.DecrementPersistentMeta(event.data['key'], event.data['value'], clientId).GetAwaiter().GetResult();
                        } else {
                            metaService.SetPersistentMeta(event.data['key'], event.data['value'], clientId).GetAwaiter().GetResult();
                        }
                        sendEvent(server, false, 'SetClientDataCompleted', 'Meta', {ClientNumber: event.clientNumber}, undefined,{status: 'Complete'});
                    } catch (error) {
                        sendEvent(server, false, 'SetClientDataCompleted', 'Meta', {ClientNumber: event.clientNumber}, undefined,{status: 'Fail'});
                    }
                }
            }
        }

        try {
            server.SetServerDvar(inDvar, '');
        } catch (error) {
            logger.WriteError(`Could not reset in bus value for ${server.EndPoint} - ${error}`);
        }
    } else if (server.ClientNum === 0) {
        servers[server.EndPoint].timer.Stop();
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
        origin.Tell("Game interface is not enabled on this server");
    }
    return enabled;
}
