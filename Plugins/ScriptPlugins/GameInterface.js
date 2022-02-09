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
        const eventType = eventTypes[gameEvent.Type];

        if (servers[server.EndPoint] != null && !servers[server.EndPoint].enabled) {
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
                if (servers[server.EndPoint] == null) {
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
            case 'disconnect':
                if (server.ClientNum === 0 && servers[server.EndPoint] != null) {
                    servers[server.EndPoint].timer.Stop();
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
    // required
    name: 'giveweapon',
    // required
    description: 'gives specified weapon',
    // required
    alias: 'gw',
    // required
    permission: 'SeniorAdmin',
    // optional (defaults to false)
    targetRequired: false,
    // optional
    arguments: [{
        name: 'weapon name',
        required: true
    }],
    supportedGames: ['IW4'],
    // required
    execute: (gameEvent) => {
        sendScriptCommand(gameEvent.Owner, 'GiveWeapon', gameEvent.Origin, {weaponName: gameEvent.Data});
    }
},
    {
        // required
        name: 'takeweapons',
        // required
        description: 'take all weapons from specifies player',
        // required
        alias: 'tw',
        // required
        permission: 'SeniorAdmin',
        // optional (defaults to false)
        targetRequired: true,
        // optional
        arguments: [],
        supportedGames: ['IW4'],
        // required
        execute: (gameEvent) => {
            sendScriptCommand(gameEvent.Owner, 'TakeWeapons', gameEvent.Target, undefined);
        }
    },
    {
        // required
        name: 'switchteam',
        // required
        description: 'switches specified player to the opposite team',
        // required
        alias: 'st',
        // required
        permission: 'Administrator',
        // optional (defaults to false)
        targetRequired: true,
        // optional
        arguments: [{
            name: 'player',
            required: true
        }],
        supportedGames: ['IW4'],
        // required
        execute: (gameEvent) => {
            sendScriptCommand(gameEvent.Owner, 'SwitchTeams', gameEvent.Target, undefined);
        }
    },
    {
        // required
        name: 'hide',
        // required
        description: 'hide yourself',
        // required
        alias: 'hi',
        // required
        permission: 'SeniorAdmin',
        // optional (defaults to false)
        targetRequired: false,
        // optional
        arguments: [],
        supportedGames: ['IW4'],
        // required
        execute: (gameEvent) => {
            sendScriptCommand(gameEvent.Owner, 'Hide', gameEvent.Origin, undefined);
        }
    },
    {
        // required
        name: 'unhide',
        // required
        description: 'unhide yourself',
        // required
        alias: 'unh',
        // required
        permission: 'SeniorAdmin',
        // optional (defaults to false)
        targetRequired: false,
        // optional
        arguments: [],
        supportedGames: ['IW4'],
        // required
        execute: (gameEvent) => {
            sendScriptCommand(gameEvent.Owner, 'Unhide', gameEvent.Origin, undefined);
        }
    },
    {
        // required
        name: 'alert',
        // required
        description: 'alert a player',
        // required
        alias: 'alr',
        // required
        permission: 'SeniorAdmin',
        // optional (defaults to false)
        targetRequired: true,
        // optional
        arguments: [{
            name: 'player',
            required: true
        },
        {
            name: 'message',
            required: true
        }],
        supportedGames: ['IW4'],
        // required
        execute: (gameEvent) => {
            sendScriptCommand(gameEvent.Owner, 'Alert', gameEvent.Target, {
                alertType: 'Alert',
                message: gameEvent.Data
            });
        }
    }];

const sendScriptCommand = (server, command, target, data) => {
    const state = servers[server.EndPoint];
    if (state === undefined || !state.enabled) {
        return;
    }
    sendEvent(server, false, 'ExecuteCommandRequested', command, target, data);
}

const sendEvent = (server, responseExpected, event, subtype, client, data) => {
    const logger = _serviceResolver.ResolveService('ILogger');

    let pendingOut = true;
    let pendingCheckCount = 0;
    const start = new Date();
    
    while (pendingOut && pendingCheckCount <= 30) {
        try {
            const out = server.GetServerDvar(outDvar);
            pendingOut =  !(out == null || out === '' || out === 'null');
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

    const output = `${responseExpected ? '1' : '0'};${event};${subtype};${client.ClientNumber};${buildDataString(data)}`;
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
        data: eventInfo.length > 4 ? eventInfo [4] : undefined
    }
}

const initialize = (server) => {
    const logger = _serviceResolver.ResolveService('ILogger');
    let enabled = false;
    try {
        enabled = server.GetServerDvar('sv_iw4madmin_integration_enabled') === '1';
    } catch (error) {
        logger.WriteError(`Could not get integration status of ${server.EndPoint} - ${error}`);
    }

    logger.WriteInfo(`GSC Integration enabled = ${enabled}`);

    if (!enabled) {
        servers[server.EndPoint] = {
            enabled: false
        }
        return false;
    }

    logger.WriteDebug(`Setting up bus timer for ${server.EndPoint}`);

    let timer = _serviceResolver.ResolveService('IScriptPluginTimerHelper');
    timer.OnTick(() => pollForEvents(server), `GameEventPoller ${server.ToString()}`);
    // necessary to prevent multi-threaded access to the JS context
    timer.SetDependency(_lock);

    servers[server.EndPoint] = {
        timer: timer,
        enabled: true
    }

    try {
        server.SetServerDvar(inDvar, '');
        server.SetServerDvar(outDvar, '');
    } catch (error) {
        logger.WriteError(`Could set default values bus dvars for ${server.EndPoint} - ${error}`);
    }

    return true;
};

const pollForEvents = server => {
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

        if (event.eventType === 'ClientDataRequested') {
            const client = server.GetClientByNumber(event.clientNumber);

            if (client != null) {
                logger.WriteDebug(`Found client ${client.Name}`);

                let data = [];

                if (event.subType === 'Meta') {
                    const metaService = _serviceResolver.ResolveService('IMetaService');
                    const meta = metaService.GetPersistentMeta(event.data, client).Result;
                    data[event.data] = meta === null ? '' : meta.Value;
                } else {
                    data = {
                        level: client.Level,
                        lastConnection: client.LastConnection
                    };
                }

                sendEvent(server, false, 'ClientDataReceived', event.subType, client, data);
            } else {
                logger.WriteWarning(`Could not find client slot ${event.clientNumber} when processing ${event.eventType}`);
                sendEvent(server, false, 'ClientDataReceived', 'Fail', { ClientNumber: event.clientNumber }, undefined);
            }
        }

        try {
            server.SetServerDvar(inDvar, '');
        } catch (error) {
            logger.WriteError(`Could not reset in bus value for ${server.EndPoint} - ${error}`);
        }
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
