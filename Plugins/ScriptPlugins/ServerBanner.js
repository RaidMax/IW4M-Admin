const init = (registerNotify, serviceResolver, config, scriptHelper) => {
    registerNotify('IGameServerEventSubscriptions.MonitoringStarted', (monitorStartEvent, _) => plugin.onServerMonitoringStart(monitorStartEvent));
    plugin.onLoad(serviceResolver, config, scriptHelper);
    return plugin;
};

const serverLocationCache = [];
const serverOrderCache = [];

const plugin = {
    author: 'RaidMax',
    version: '1.1',
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

        this.logger.logInformation('{Name} {Version} by {Author} loaded,', this.name, this.version,
            this.author);
    },

    onServerMonitoringStart: function (startEvent) {
        let lookupComplete = true;
        if (serverLocationCache[startEvent.server.listenAddress] === undefined) {
            serverLocationCache[startEvent.server.listenAddress] = 'SO';
            lookupComplete = false;
        }

        if (serverOrderCache[startEvent.server.gameCode] === undefined) {
            serverOrderCache[startEvent.server.gameCode] = [];
        }

        serverOrderCache[startEvent.server.gameCode].push(startEvent.server);
        serverOrderCache[startEvent.server.gameCode].sort((a, b) => b.clientNum - a.clientNum);
        serverOrderCache.sort((a, b) => b[Object.keys(b)[0].clientNum] - b[Object.keys(a)[0].clientNum]);

        if (lookupComplete) {
            return;
        }

        const lookupIp = startEvent.server.resolvedIpEndPoint.address.isInternal() ?
            this.manager.externalIPAddress :
            startEvent.server.resolvedIpEndPoint.toString().split(':')[0];

        this.logger.logInformation('Looking up server location for IP {IP}', lookupIp);

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

            interactionData.scriptAction = (sourceId, targetId, game, meta, _) => {
                const serverId = meta.serverId;
                const isSmall = meta.size !== undefined && meta.size === 'small';
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
                    'h2m': {
                        right: colorLeft
                    },
                    'iw7': {
                        right: colorLeft
                    }
                };

                const servers = plugin.manager.servers;
                for (let i = 0; i < servers.length; i++) {
                    if (servers[i].id === serverId) {
                        server = servers[i];
                        break;
                    }
                }

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
                let status = isSmall ? '<div class="status small online-checkmark"></div>' : '<div class="status-online subtitle">ONLINE</div>';
                if (server.throttled) {
                    status = isSmall ? '<div class="status small offline-x"></div>' : '<div class="status-offline subtitle">OFFLINE</div>';
                }

                const displayIp = server.resolvedIpEndPoint.address.isInternal() ?
                    plugin.manager.externalIPAddress :
                    server.resolvedIpEndPoint.toString().split(':')[0];

                const head = `<head>
                    <link rel="stylesheet" href="https://fonts.googleapis.com/css?family=${font}">
                        <style>
                            * {
                                padding: 0;
                                margin: 0;
                            }
                            .server-container {
                                font-family: '${font}';
                                background: url('https://raidmax.org/resources/images/banners/${gameCode}.jpg') no-repeat;
                                align-items: center;
                            }
                            .server-container.large {
                                padding-left: 1rem;
                                padding-right: 1rem;
                                width: calc(750px - 2rem);
                                height: 120px;
                                display: flex;
                                background-position: center center;
                            }
                            .server-container.small {
                                padding: 0.5rem;
                                background-position: left center;
                            }
                            .game-icon {
                                background: url('https://raidmax.org/resources/images/icons/games/${gameCode}.jpg') no-repeat;
                                background-size: contain;
                            }
                            .game-icon.large {
                                width: 64px;
                                height: 64px;
                                border-radius: 10px;
                            }
                            .game-icon.small {
                                width: 20px;
                                height: 20px;
                                border-radius: 5px;
                            }
                            .first-line.small, .second-line.small {
                                display: flex; 
                                flex-direction: row;
                            }
                            .first-line.small .header {
                                font-size: 10pt;
                                font-weight: bold;
                                margin-left: 0.5rem;
                                align-self: center;
                            }
                            .second-line.small {
                                align-self: center;
                            }
                            .game-info.small {
                                margin-left: 0.5rem;
                                font-size: 9pt;
                            }
                            .game-info.large {
                                padding: 0 0.75em;
                            }
                            .game-info .header {
                                font-weight: bold;
                            }
                            img.location-image {
                                width: 20px;
                                align-self: center;
                            }
                            .game-info.large .subtitle {
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
                            .status.small:after {
                                position: absolute;
                                width: 20px;
                                height: 15px;
                                text-align: center;
                                border-radius: 2px;
                                font-size: 8pt;
                                margin-top: 0.1rem;
                                margin-left: -0.5rem;
                            }
                            .online-checkmark:after {
                                content: '\\2714';
                                color: white;
                                background: rgba(0, 128, 0, 0.5);
                            }
                            .offline-x:after {
                                content: '\\2715';
                                color: white;
                                background: rgba(128, 0, 0, 0.5);
                            }
                            h3, .server-container.large div {
                                line-height: 1.5rem;
                            }
                            h2 {
                                line-height: 2rem;
                            }
                        </style>
                        <title>${displayIp}:${server.listenPort}</title>
                </head>`;


                if (isSmall) {
                    return `<html lang="en">
                                ${head}
                                <body>
                                    <div class="server-container small" id="server">
                                        <div class="first-line small"> 
                                            <div class="game-icon small"></div>
                                            <div class="header" id="serverName" style="${colorLeft}"></div>
                                        </div>
                                        <div class="third-line game-info small">
                                            ${status}
                                            <div style="${colorLeft}; margin-left: 20px;">${displayIp}:${server.listenPort}</div>
                                        </div>
                                        <div class="second-line small">
                                            <img src="https://flagcdn.com/w40/${serverLocationCache[server.listenAddress]?.toLowerCase()}.png" 
                                                 alt="${serverLocationCache[server.listenAddress]}" class="location-image">
                                            <div class="game-info small" style="${colorLeft}">
                                                <span>${server.throttled ? '-' : server.clientNum}/${server.maxClients}</span>
                                                &bullet;
                                                <span>${server.map.alias}</span>
                                                &bullet;
                                                <span>${server.gametypeName}</span>   
                                            </div>
                                        </div> 
                                    </div>
                                    <script>
                                        let serverNameElem = document.getElementById('serverName');
                                        serverNameElem.textContent = '${server.serverName.stripColors()}';
                                    </script>
                                </body>
                            </html>`;
                }

                return `<html lang="en">
                            ${head}
                            <body>
                                <div class="server-container large" id="server">
                                        <div class="game-icon large" 
                                            style="background: url('https://raidmax.org/resources/images/icons/games/${gameCode}.jpg');">
                                        </div>
                                        <div style="flex: 1; ${colorLeft}" class="game-info large">
                                            <div class="header" id="serverName"></div>
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
                                <script>
                                    let serverNameElem = document.getElementById('serverName');
                                    serverNameElem.textContent = '${server.serverName.stripColors()}';
                                </script>
                            </body>
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
            interactionData.displayMeta = 'ph-image';

            interactionData.scriptAction = (_, __, ___, ____, _____) => {
                if (Object.keys(serverOrderCache).length === 0) {
                    for (let i = 0; i < plugin.manager.servers.length; i++) {
                        const server = plugin.manager.servers[i];
                        plugin.onServerMonitoringStart({
                            server: server
                        });
                    }
                }

                let response = '<div class="grid grid-cols-1 xl:grid-cols-2 gap-6">';
                Object.keys(serverOrderCache).forEach(key => {
                    const servers = serverOrderCache[key];
                    for (let i = 0; i < servers.length; i++) {
                        const eachServer = servers[i];
                        response += `<div class="bg-surface rounded-lg border border-line shadow-sm p-5">
                                        <div class="text-base mb-4 flex items-center justify-between">
                                            <span id="serverName" class="font-medium text-foreground"></span>
                                            <span class="px-2 py-1 rounded bg-surface-alt text-sm font-mono text-muted border border-line">${eachServer.gameCode}</span>
                                        </div>
                                 
                                        <div class="overflow-hidden rounded-lg mb-4">
                                            <iframe src="/Interaction/Render/Banner?serverId=${eachServer.id}" width="750" 
                                                    height="120" style="border-width: 0; overflow: hidden;" 
                                                    title="${eachServer.id}"></iframe>
                                        </div>
                                        <button type="button" class="px-4 py-2 rounded-lg bg-primary text-white hover:bg-primary-hover transition-colors text-sm font-medium mb-3" onclick="document.getElementById('showCode${eachServer.id}').classList.toggle('hidden')">Show Embed</button>
                                        <div class="hidden p-4 mb-4 bg-surface-alt rounded-lg border border-line font-mono text-xs text-muted overflow-x-auto" id="showCode${eachServer.id}">
                                            &lt;iframe 
                                            <br/>&nbsp;src="${plugin.webfrontUrl}/Interaction/Render/Banner?serverId=${eachServer.id}" 
                                            <br/>&nbsp;width="750" height="120" style="border-width: 0; overflow: hidden;"&gt;<br/>
                                            &lt;/iframe&gt;</div>
                                        <div class="rounded-lg overflow-hidden mb-4">
                                            <iframe src="/Interaction/Render/Banner?serverId=${eachServer.id}&size=small" width="400"
                                                    height="70" style="border-width: 0; overflow: hidden;" 
                                                    title="${eachServer.id}"></iframe>
                                        </div>
                                        <button type="button" class="px-4 py-2 rounded-lg bg-primary text-white hover:bg-primary-hover transition-colors text-sm font-medium mb-3" onclick="document.getElementById('showCode${eachServer.id}Small').classList.toggle('hidden')">Show Embed</button>
                                        <div class="hidden p-4 bg-surface-alt rounded-lg border border-line font-mono text-xs text-muted overflow-x-auto" id="showCode${eachServer.id}Small">
                                            &lt;iframe 
	                                        <br/>&nbsp;src="${plugin.webfrontUrl}/Interaction/Render/Banner?serverId=${eachServer.id}&size=small" 
                                            <br/>&nbsp;width="400" height="70" style="border-width: 0; overflow: hidden;"&gt;<br/>
                                            &lt;/iframe&gt;</div>
                                    </div>
                                    <script>
                                        let serverNameElem = document.getElementById('serverName');
                                        serverNameElem.textContent = '${eachServer.serverName.stripColors()}';
                                    </script>`;
                    }
                });

                response += '</div>';
                return response;
            };

            return interactionData;
        }
    }]
};
