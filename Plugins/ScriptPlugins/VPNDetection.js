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
            let httpRequest = System.Net.WebRequest.Create('https://api.xdefcon.com/proxy/check/?ip=' + origin.IPAddressString);
            let response = httpRequest.GetResponse();
            let data = response.GetResponseStream();
            let streamReader = new System.IO.StreamReader(data);
            let jsonResponse = streamReader.ReadToEnd();
            streamReader.Dispose();
            response.Close();
            let parsedJSON = JSON.parse(jsonResponse);
            usingVPN = parsedJSON['success'] && parsedJSON['proxy'];
        } catch (e) {
            this.logger.WriteError(e.message);
        }

        if (usingVPN) {
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