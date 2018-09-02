const plugin = {
    author: 'RaidMax',
    version: 1.0,
    name: 'VPN Kick Plugin',

    manager: null,
    logger: null,
    vpnExceptionIds: [],

    checkForVpn(origin) {
        let exempt = false;
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

        let usingVPN = false;

        try {
            let cl = new System.Net.Http.HttpClient();
            let re = cl.GetAsync('https://api.xdefcon.com/proxy/check/?ip=' + origin.IPAddressString).Result;
            let co = re.Content;
            let parsedJSON = JSON.parse(co.ReadAsStringAsync().Result);
            //co.Dispose();
            //re.Dispose();
            //cl.Dispose();
            usingVPN = parsedJSON['success'] && parsedJSON['proxy'];
        } catch (e) {
            this.logger.WriteError(e.message);
        }

        if (usingVPN) {
            this.logger.WriteInfo(origin + ' is using a VPN (' + origin.IPAddressString + ')');
            let library = importNamespace('SharedLibraryCore');
            let kickOrigin = new library.Objects.Player();
            kickOrigin.ClientId = 1;
            origin.Kick(_localization.LocalizationIndex["SERVER_KICK_VPNS_NOTALLOWED"], kickOrigin);
        }
    },

    onEventAsync(gameEvent, server) {
        // connect event
        if (gameEvent.Type === 3) {
            this.checkForVpn(gameEvent.Origin);
        }
    },

    onLoadAsync(manager) {
        this.manager = manager;
        this.logger = manager.GetLogger();
    },

    onUnloadAsync() { },

    onTickAsync(server) { }
};