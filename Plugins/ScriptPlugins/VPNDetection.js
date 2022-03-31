const commands = [{
    name: "whitelistvpn",
    description: "whitelists a player's client id from VPN detection",
    alias: "wv",
    permission: "SeniorAdmin",
    targetRequired: true,
    arguments: [{
        name: "player",
        required: true
    }],
    execute: (gameEvent) => {
        plugin.vpnExceptionIds.push(gameEvent.Target.ClientId);
        plugin.configHandler.SetValue('vpnExceptionIds', plugin.vpnExceptionIds);

        gameEvent.Origin.Tell(`Successfully whitelisted ${gameEvent.Target.Name}`);
    }
}];

const plugin = {
    author: 'RaidMax',
    version: 1.3,
    name: 'VPN Detection Plugin',
    manager: null,
    logger: null,
    vpnExceptionIds: [],

    checkForVpn: function (origin) {
        let exempt = false;
        // prevent players that are exempt from being kicked
        this.vpnExceptionIds.forEach(function (id) {
            if (id === origin.ClientId) {
                exempt = true;
                return false;
            }
        });

        if (exempt) {
            this.logger.WriteInfo(`${origin} is whitelisted, so we are not checking VPN status`);
            return;
        }

        let usingVPN = false;

        try {
            const cl = new System.Net.Http.HttpClient();
            const re = cl.GetAsync(`https://api.xdefcon.com/proxy/check/?ip=${origin.IPAddressString}`).Result;
            const userAgent = `IW4MAdmin-${this.manager.GetApplicationSettings().Configuration().Id}`;
            cl.DefaultRequestHeaders.Add('User-Agent', userAgent);
            const co = re.Content;
            const parsedJSON = JSON.parse(co.ReadAsStringAsync().Result);
            co.Dispose();
            re.Dispose();
            cl.Dispose();
            usingVPN = parsedJSON.success && parsedJSON.proxy;
        } catch (e) {
            this.logger.WriteWarning(`There was a problem checking client IP for VPN ${e.message}`);
        }

        if (usingVPN) {
            this.logger.WriteInfo(origin + ' is using a VPN (' + origin.IPAddressString + ')');
            const contactUrl = this.manager.GetApplicationSettings().Configuration().ContactUri;
            let additionalInfo = '';
            if (contactUrl) {
                additionalInfo = _localization.LocalizationIndex['SERVER_KICK_VPNS_NOTALLOWED_INFO'] + ' ' + contactUrl;
            }
            origin.Kick(_localization.LocalizationIndex['SERVER_KICK_VPNS_NOTALLOWED'] + ' ' + additionalInfo, _IW4MAdminClient);
        }
    },

    onEventAsync: function (gameEvent, server) {
        // join event
        if (gameEvent.TypeName === 'Join') {
            this.checkForVpn(gameEvent.Origin);
        }
    },

    onLoadAsync: function (manager) {
        this.manager = manager;
        this.logger = manager.GetLogger(0);

        this.configHandler = _configHandler;
        this.configHandler.GetValue('vpnExceptionIds').forEach(element => this.vpnExceptionIds.push(element));
        this.logger.WriteInfo(`Loaded ${this.vpnExceptionIds.length} ids into whitelist`);
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
