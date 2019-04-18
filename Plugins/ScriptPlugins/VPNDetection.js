var plugin = {
    author: 'RaidMax',
    version: 1.1,
    name: 'VPN Detection Plugin',

    manager: null,
    logger: null,
    vpnExceptionIds: [],

    checkForVpn: function (origin) {
        var exempt = false;
        // prevent players that are exempt from being kicked
        this.vpnExceptionIds.forEach(function (id) {
            if (id === origin.ClientId) {
                exempt = true;
                return false;
            }
        });

        if (exempt) {
            return;
        }

        var usingVPN = false;

        try {
            var cl = new System.Net.Http.HttpClient();
            var re = cl.GetAsync('https://api.xdefcon.com/proxy/check/?ip=' + origin.IPAddressString).Result;
            var co = re.Content;
            var parsedJSON = JSON.parse(co.ReadAsStringAsync().Result);
            co.Dispose();
            re.Dispose();
            cl.Dispose();
            usingVPN = parsedJSON.success && parsedJSON.proxy;
        } catch (e) {
            this.logger.WriteWarning('There was a problem checking client IP for VPN ' + e.message);
        }

        if (usingVPN) {
            this.logger.WriteInfo(origin + ' is using a VPN (' + origin.IPAddressString + ')');
            origin.Kick(_localization.LocalizationIndex["SERVER_KICK_VPNS_NOTALLOWED"], _IW4MAdminClient);
        }
    },

    onEventAsync: function (gameEvent, server) {
        // connect event
        if (gameEvent.Type === 3) {
            this.checkForVpn(gameEvent.Origin);
        }
    },

    onLoadAsync: function (manager) {
        this.manager = manager;
        this.logger = manager.GetLogger(0);
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};