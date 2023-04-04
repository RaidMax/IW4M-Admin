const cidrRegex = /^([0-9]{1,3}\.){3}[0-9]{1,3}(\/([0-9]|[1-2][0-9]|3[0-2]))?$/;
const validCIDR = input => cidrRegex.test(input);
const subnetBanlistKey = 'Webfront::Nav::Admin::SubnetBanlist';
let subnetList = [];

const init = (registerNotify, serviceResolver, config) => {
    registerNotify('IManagementEventSubscriptions.ClientStateAuthorized', (authorizedEvent, _) => plugin.onClientAuthorized(authorizedEvent));

    plugin.onLoad(serviceResolver, config);
    return plugin;
};

const plugin = {
    author: 'RaidMax',
    version: '2.0',
    name: 'Subnet Banlist Plugin',
    manager: null,
    logger: null,
    config: null,
    serviceResolver: null,
    banMessage: '',

    commands: [{
        name: 'bansubnet',
        description: 'bans an IPv4 subnet',
        alias: 'bs',
        permission: 'SeniorAdmin',
        targetRequired: false,
        arguments: [{
            name: 'subnet in IPv4 CIDR notation',
            required: true
        }],

        execute: (gameEvent) => {
            const input = String(gameEvent.data).trim();

            if (!validCIDR(input)) {
                gameEvent.origin.tell('Invalid CIDR input');
                return;
            }

            subnetList.push(input);
            plugin.config.setValue('SubnetBanList', subnetList);

            gameEvent.origin.tell(`Added ${input} to subnet banlist`);
        }
    },
        {
            name: 'unbansubnet',
            description: 'unbans an IPv4 subnet',
            alias: 'ubs',
            permission: 'SeniorAdmin',
            targetRequired: false,
            arguments: [{
                name: 'subnet in IPv4 CIDR notation',
                required: true
            }],
            execute: (gameEvent) => {
                const input = String(gameEvent.data).trim();

                if (!validCIDR(input)) {
                    gameEvent.origin.tell('Invalid CIDR input');
                    return;
                }

                if (!subnetList.includes(input)) {
                    gameEvent.origin.tell('Subnet is not banned');
                    return;
                }

                subnetList = subnetList.filter(item => item !== input);
                plugin.config.setValue('SubnetBanList', subnetList);

                gameEvent.origin.tell(`Removed ${input} from subnet banlist`);
            }
        }
    ],

    interactions: [{
        name: subnetBanlistKey,
        action: function (_, __, ___) {
            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.name = 'Subnet Banlist'; // navigation link name
            interactionData.description = `List of banned subnets (${subnetList.length} Total)`; // alt and title
            interactionData.displayMeta = 'oi-circle-x'; // nav icon
            interactionData.interactionId = subnetBanlistKey;
            interactionData.minimumPermission = 3;
            interactionData.interactionType = 2;
            interactionData.source = plugin.name;

            interactionData.ScriptAction = (sourceId, targetId, game, meta, token) => {
                let table = '<table class="table bg-dark-dm bg-light-lm">';

                const unbanSubnetInteraction = {
                    InteractionId: 'command',
                    Data: 'unbansubnet',
                    ActionButtonLabel: 'Unban',
                    Name: 'Unban Subnet'
                };

                subnetList.forEach(subnet => {
                    unbanSubnetInteraction.Data += ' ' + subnet;
                    table += `<tr>
                                    <td>
                                        <p>${subnet}</p>
                                    </td>
                                    <td>
                                        <a href="#" class="profile-action no-decoration float-right" data-action="DynamicAction"
                                           data-action-meta="${encodeURI(JSON.stringify(unbanSubnetInteraction))}"> 
                                            <div class="btn">
                                                <i class="oi oi-circle-x mr-5 font-size-12"></i>
                                                <span class="text-truncate">Unban Subnet</span>
                                            </div>
                                        </a>
                                    </td>
                                </tr>`;
                });

                table += '</table>';

                return table;
            };

            return interactionData;
        }
    }],

    onLoad: function (serviceResolver, config) {
        this.serviceResolver = serviceResolver;
        this.config = config;
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        subnetList = [];

        const list = this.config.getValue('SubnetBanList');
        if (list !== undefined) {
            list.forEach(element => {
                const ban = String(element);
                subnetList.push(ban);
            });
            this.logger.logInformation('Loaded {Count} banned subnets', list.length);
        } else {
            this.config.setValue('SubnetBanList', []);
        }

        this.banMessage = this.config.getValue('BanMessage');

        if (this.banMessage === undefined) {
            this.banMessage = 'You are not allowed to join this server.';
            this.config.setValue('BanMessage', this.banMessage);
        }

        const interactionRegistration = serviceResolver.resolveService('IInteractionRegistration');
        interactionRegistration.unregisterInteraction(subnetBanlistKey);

        this.logger.logInformation('Subnet Ban loaded');
    },

    onClientAuthorized: (clientEvent) => {
        if (!isSubnetBanned(clientEvent.client.ipAddressString, subnetList)) {
            return;
        }

        this.logger.logInformation(`Kicking {Client} because they are subnet banned.`, clientEvent.client);
        clientEvent.client.kick(this.banMessage, clientEvent.client.currentServer.asConsoleClient());
    }
};

const convertIPtoLong = ip => {
    let components = String(ip).match(/^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/);
    if (components) {
        let ipLong = 0;
        let power = 1;
        for (let i = 4; i >= 1; i -= 1) {
            ipLong += power * parseInt(components[i]);
            power *= 256;
        }
        return ipLong;
    } else {
        return -1;
    }
};

const isInSubnet = (ip, subnet) => {
    const mask = subnet.match(/^(.*?)\/(\d{1,2})$/);

    if (!mask) {
        return false;
    }

    const baseIP = convertIPtoLong(mask[1]);
    const longIP = convertIPtoLong(ip);

    if (mask && baseIP >= 0) {
        const freedom = Math.pow(2, 32 - parseInt(mask[2]));
        return (longIP > baseIP) && (longIP < baseIP + freedom - 1);
    } else return false;
};

const isSubnetBanned = (ip, list) => {
    const matchingSubnets = list.filter(subnet => isInSubnet(ip, subnet));
    return matchingSubnets.length !== 0;
};
