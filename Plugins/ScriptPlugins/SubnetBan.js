const cidrRegex = /^([0-9]{1,3}\.){3}[0-9]{1,3}(\/([0-9]|[1-2][0-9]|3[0-2]))?$/;
const validCIDR = input => cidrRegex.test(input);
const subnetBanlistKey = 'Webfront::Nav::Admin::SubnetBanlist';
let subnetList = [];

const commands = [{
    name: "bansubnet",
    description: "bans an IPv4 subnet",
    alias: "bs",
    permission: "SeniorAdmin",
    targetRequired: false,
    arguments: [{
        name: "subnet in IPv4 CIDR notation",
        required: true
    }],

    execute: (gameEvent) => {
        const input = String(gameEvent.Data).trim();

        if (!validCIDR(input)) {
            gameEvent.Origin.Tell('Invalid CIDR input');
            return;
        }

        subnetList.push(input);
        _configHandler.SetValue('SubnetBanList', subnetList);

        gameEvent.Origin.Tell(`Added ${input} to subnet banlist`);
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
            const input = String(gameEvent.Data).trim();

            if (!validCIDR(input)) {
                gameEvent.Origin.Tell('Invalid CIDR input');
                return;
            }

            if (!subnetList.includes(input)) {
                gameEvent.Origin.Tell('Subnet is not banned');
                return;
            }

            subnetList = subnetList.filter(item => item !== input);
            _configHandler.SetValue('SubnetBanList', subnetList);

            gameEvent.Origin.Tell(`Removed ${input} from subnet banlist`);
        }
    }];

convertIPtoLong = ip => {
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

isInSubnet = (ip, subnet) => {
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

isSubnetBanned = (ip, list) => {
    const matchingSubnets = list.filter(subnet => isInSubnet(ip, subnet));
    return matchingSubnets.length !== 0;
}

const plugin = {
    author: 'RaidMax',
    version: 1.1,
    name: 'Subnet Banlist Plugin',
    manager: null,
    logger: null,
    banMessage: '',

    onEventAsync: (gameEvent, server) => {
        if (gameEvent.TypeName === 'Join') {
            if (!isSubnetBanned(gameEvent.Origin.IPAddressString, subnetList, this.logger)) {
                return;
            }

            this.logger.WriteInfo(`Kicking ${gameEvent.Origin} because they are subnet banned.`);
            gameEvent.Origin.Kick(this.banMessage, _IW4MAdminClient);
        }
    },
    onLoadAsync: manager => {
        this.manager = manager;
        this.logger = manager.GetLogger(0);
        this.configHandler = _configHandler;
        subnetList = [];
        this.interactionRegistration = _serviceResolver.ResolveService('IInteractionRegistration');

        const list = this.configHandler.GetValue('SubnetBanList');
        if (list !== undefined) {
            list.forEach(element => {
                const ban = String(element);
                subnetList.push(ban)
            });
            this.logger.WriteInfo(`Loaded ${list.length} banned subnets`);
        } else {
            this.configHandler.SetValue('SubnetBanList', []);
        }

        this.banMessage = this.configHandler.GetValue('BanMessage');

        if (this.banMessage === undefined) {
            this.banMessage = 'You are not allowed to join this server.';
            this.configHandler.SetValue('BanMessage', this.banMessage);
        }

        this.interactionRegistration.RegisterScriptInteraction(subnetBanlistKey, plugin.name, (targetId, game, token) => {
            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.Name = 'Subnet Banlist'; // navigation link name
            interactionData.Description = `List of banned subnets (${subnetList.length} Total)`;  // alt and title
            interactionData.DisplayMeta = 'oi-circle-x'; // nav icon
            interactionData.InteractionId = subnetBanlistKey;
            interactionData.MinimumPermission = 3; // moderator
            interactionData.InteractionType = 2; // 1 is RawContent for apis etc..., 2 is 
            interactionData.Source = plugin.name;

            interactionData.ScriptAction = (sourceId, targetId, game, meta, token) => {
                let table = '<table class="table bg-dark-dm bg-light-lm">';

                const unbanSubnetInteraction = {
                    InteractionId: 'command',
                    Data: 'unbansubnet',
                    ActionButtonLabel: 'Unban',
                    Name: 'Unban Subnet'
                };

                subnetList.forEach(subnet => {
                    unbanSubnetInteraction.Data += ' ' + subnet
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
            }

            return interactionData;
        });
    },

    onUnloadAsync: () => {
        this.interactionRegistration.UnregisterInteraction(subnetBanlistKey);
    },

    onTickAsync: server => {
    }
};
