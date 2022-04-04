const cidrRegex = /^([0-9]{1,3}\.){3}[0-9]{1,3}(\/([0-9]|[1-2][0-9]|3[0-2]))?$/;
const validCIDR = input => cidrRegex.test(input);

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

        plugin.subnetList.push(input);
        _configHandler.SetValue('SubnetBanList', plugin.subnetList);

        gameEvent.Origin.Tell(`Added ${input} to subnet banlist`);
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
    version: 1.0,
    name: 'Subnet Banlist Plugin',
    manager: null,
    logger: null,
    banMessage: '',

    onEventAsync: (gameEvent, server) => {
        if (gameEvent.TypeName === 'Join') {
            if (!isSubnetBanned(gameEvent.Origin.IPAddressString, this.subnetList, this.logger)) {
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
        this.subnetList = [];

        const list = this.configHandler.GetValue('SubnetBanList');
        if (list !== undefined) {
            list.forEach(element => {
                const ban = String(element);
                this.subnetList.push(ban)
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
    },

    onUnloadAsync: () => {
    },

    onTickAsync: server => {
    }
};
