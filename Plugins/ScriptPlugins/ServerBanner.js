const init = (registerNotify, serviceResolver, config, scriptHelper) => {
    registerNotify('IGameServerEventSubscriptions.MonitoringStarted', (monitorStartEvent, _) => plugin.onServerMonitoringStart(monitorStartEvent));
    plugin.onLoad(serviceResolver, config, scriptHelper);
    return plugin;
};

const serverLocationCache = [];
const serverOrderCache = [];

const plugin = {
    author: 'RaidMax',
    version: '1.0',
    name: 'Server Banner',
    serviceResolver: null,
    scriptHelper: null,
    config: null,
    manager: null,
    logger: null,
    webfrontUrl: null,

    onLoad: function (serviceResolver, config, scriptHelper) {
        this.serviceResolver = serviceResolver;
        this.config = config;
        this.scriptHelper = scriptHelper;

        this.manager = serviceResolver.resolveService('IManager');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.webfrontUrl = serviceResolver.resolveService('ApplicationConfiguration').webfrontUrl;
    },

    onServerMonitoringStart: function (startEvent) {
        if (serverLocationCache[startEvent.server.listenAddress] === undefined) {
            serverLocationCache[startEvent.server.listenAddress] = 'UA';
        }

        if (serverOrderCache[startEvent.server.gameCode] === undefined) {
            serverOrderCache[startEvent.server.gameCode] = [];
        }

        const lookupIp = startEvent.server.resolvedIpEndPoint.address.isInternal() ?
            this.manager.externalIPAddress :
            startEvent.server.listenAddress;

        serverOrderCache[startEvent.server.gameCode].push(startEvent.server);
        serverOrderCache[startEvent.server.gameCode].sort((a, b) => b.clientNum - a.clientNum);

        this.scriptHelper.getUrl(`https://ipinfo.io/${lookupIp}/country`, (result) => {
            let error = true;

            try {
                JSON.parse(result);
            } catch {
                error = false;
            }

            if (!error) {
                serverLocationCache[startEvent.server.listenAddress] = String(result);
            } else {
                this.logger.logWarning('Could not determine server location from IP');
            }
        });
    },

    interactions: [{
        name: 'Banner',
        action: function (_, __, ___) {
            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.interactionId = 'banner';
            interactionData.minimumPermission = 0;
            interactionData.interactionType = 1;
            interactionData.source = plugin.name;

            interactionData.scriptAction = (sourceId, targetId, game, meta, token) => {
                const serverId = meta['serverId'];
                let server;

                let colorLeft = 'color: #f5f5f5; text-shadow: -1px 1px 8px #000000cc;';
                let colorRight = 'color: #222222; text-shadow: -1px 1px 8px #ecececcc;';

                const colorMappingOverride = {
                    't6': {
                        right: colorLeft
                    },
                    'iw3': {
                        left: colorRight,
                    },
                    'iw5': {
                        left: colorRight
                    },
                    'iw6': {
                        right: colorLeft
                    },
                    't4': {
                        left: colorRight,
                        right: colorLeft
                    },
                    't5': {
                        right: colorLeft
                    },
                    't7': {
                        right: colorLeft
                    },
                    'shg1': {
                        right: colorLeft
                    },
                    'h1': {
                        right: colorLeft
                    },
                    'csgo': {
                        right: colorLeft
                    },
                };

                plugin.manager.getServers().forEach(eachServer => {
                    if (eachServer.id === serverId) {
                        server = eachServer;
                    }
                });

                if (serverLocationCache[server.listenAddress] === undefined) {
                    plugin.onServerMonitoringStart({
                        server: server
                    });
                }

                if (serverOrderCache[server.gameCode] === undefined) {
                    plugin.onServerMonitoringStart({
                        server: server
                    });
                }

                let gameCode = server.gameCode.toLowerCase();
                colorLeft = colorMappingOverride[gameCode]?.left || colorLeft;
                colorRight = colorMappingOverride[gameCode]?.right || colorRight;

                const font = 'Noto Sans Mono';
                let status = '<div class="status-online subtitle">ONLINE</div>';
                if (server.throttled) {
                    status = '<div class="status-offline subtitle">OFFLINE</div>';
                }

                const displayIp = server.resolvedIpEndPoint.address.isInternal() ?
                    plugin.manager.externalIPAddress :
                    server.listenAddress;

                return `<html>
                            <head>
                                <link rel="stylesheet" href="https://fonts.googleapis.com/css?family=${font}">
                                <style>
                                    * {
                                        padding: 0;
                                        margin: 0;
                                    }
                                    .server-container {
                                        padding-left: 1rem;
                                        padding-right: 1rem;
                                        width: calc(750px - 2rem);
                                        height: 120px;
                                        display: flex;
                                        font-family: '${font}';
                                        background: url('https://raidmax.org/resources/images/banners/${gameCode}.jpg') no-repeat;
                                        align-items: center;
                                    }
                                    .contrast {
                                        background-color: rgba(0, 0, 0, 0.5);
                                    }
                                    .game-icon {
                                        border-radius: 10px;
                                        width: 64px;
                                        height: 64px;
                                    }
                                    .game-info {
                                        padding: 0 0.75em;
                                    }
                                    .game-info .header {
                                        font-weight: bold;
                                    }
                                    .game-info .subtitle {
                                        font-size: 0.9rem;
                                    }
                                    .text-weight-lighter {
                                        font-weight: lighter
                                    }
                                    .status-online {
                                        color: green;
                                    }
                                    .status-offline {
                                        color: red;
                                    }
                                    .players-flag-section {
                                        flex: 1; 
                                        display:flex; 
                                        flex-direction: row; 
                                        align-items: center;
                                    }
                                    .players-flag-section img {
                                        margin: 0 0.5rem;
                                        height: 0.75rem;
                                    }
                                    h3, div {
                                        line-height: 1.5rem;
                                    }
                                    h2 {
                                        line-height: 2rem;
                                    }
                                </style>
                            </head>
                           
                            <div class="server-container contrast" id="server">
                                    <div class="game-icon" 
                                        style="background: url('https://raidmax.org/resources/images/icons/games/${gameCode}.jpg');">
                                    </div>
                                    <div style="flex: 1; ${colorLeft}" class="game-info">
                                        <div class="header">${server.serverName.stripColors()}</div>
                                        <div class="text-weight-lighter subtitle">${displayIp}:${server.listenPort}</div>
                                        <div class="players-flag-section">
                                            <div class="subtitle">${server.throttled ? '-' : server.clientNum}/${server.maxClients} Players</div>
                                            <img src="https://flagcdn.com/h20/${serverLocationCache[server.listenAddress]?.toLowerCase()}.png" 
                                                 alt="${serverLocationCache[server.listenAddress]}"/>
                                        </div>
                                    </div>
                                    <div style="${colorRight}; text-align: right;" class="game-info">
                                        <div class="header">${server.map.alias}</div>
                                        <div class="text-weight-lighter subtitle">${server.gametypeName}</div>
                                        ${status}
                                    </div>
                            </div>
                        </html>`;
            };

            return interactionData;
        }
    }, {
        name: 'Webfront::Nav::Main::BannerPreview',
        action: function (_, __, ___) {
            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.interactionId = 'Webfront::Nav::Main::BannerPreview';
            interactionData.minimumPermission = 0;
            interactionData.interactionType = 2;
            interactionData.source = plugin.name;
            interactionData.name = 'Banners';
            interactionData.description = interactionData.name;
            interactionData.displayMeta = 'oi-image';

            interactionData.scriptAction = (_, __, ___, ____, _____) => {
                if (Object.keys(serverOrderCache).length === 0) {
                    plugin.manager.getServers().forEach(server => {
                        plugin.onServerMonitoringStart({
                            server: server
                        });
                    });
                }

                let response = '<div class="d-flex flex-row flex-wrap" style="margin-left: -1rem; margin-top: -1rem;">';
                Object.keys(serverOrderCache).forEach(key => {
                    const servers = serverOrderCache[key];
                    servers.forEach(eachServer => {
                        response += `<div class="w-full w-xl-half">
                                    <div class="card m-10 p-20">
                                    <div class="font-size-16 mb-10"> <div class="badge ml-10 float-right font-size-16">${eachServer.gameCode}</div>${eachServer.serverName.stripColors()}</div>
                                 
                                    <div style="overflow: hidden">
                                    <iframe src="/Interaction/Render/Banner?serverId=${eachServer.id}" width="750" height="120" style="border-width: 0; overflow: hidden;" class="rounded mb-5" ></iframe>
                                    </div>
                                    <div class="btn mb-10" onclick="document.getElementById('showCode${eachServer.id}').style.removeProperty('display')">Show Embed</div>
                                    <div class="code p-5" id="showCode${eachServer.id}" style="display:none;">&lt;iframe 
	<br/>&nbsp;src="${plugin.webfrontUrl}/Interaction/Render/Banner?serverId=${eachServer.id}" 
        <br/>&nbsp;width="750" height="120" style="border-width: 0; overflow: hidden;"&gt;<br/>
&lt;/iframe&gt;</div>
                                </div></div>`;
                    });
                });

                response += '</div>';
                return response;
            };

            return interactionData;
        }
    }]
};
