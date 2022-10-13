let vpnExceptionIds = [];
const commands = [{
    name: 'whitelistvpn',
    description: 'whitelists a player\'s client id from VPN detection',
    alias: 'wv',
    permission: 'SeniorAdmin',
    targetRequired: true,
    arguments: [{
        name: 'player',
        required: true
    }],
    execute: (gameEvent) => {
        vpnExceptionIds.push(gameEvent.Target.ClientId);
        plugin.configHandler.SetValue('vpnExceptionIds', vpnExceptionIds);

        gameEvent.Origin.Tell(`Successfully whitelisted ${gameEvent.Target.Name}`);
    }
}];

const plugin = {
    author: 'RaidMax',
    version: 1.5,
    name: 'VPN Detection Plugin',
    manager: null,
    logger: null,

    checkForVpn: function (origin) {
        let exempt = false;
        // prevent players that are exempt from being kicked
        vpnExceptionIds.forEach(function (id) {
            if (id == origin.ClientId) { // when loaded from the config the "id" type is not the same as the ClientId type
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
        this.configHandler.GetValue('vpnExceptionIds').forEach(element => vpnExceptionIds.push(parseInt(element)));
        this.logger.WriteInfo(`Loaded ${vpnExceptionIds.length} ids into whitelist`);

        this.interactionRegistration = _serviceResolver.ResolveService('IInteractionRegistration');
        this.interactionRegistration.RegisterScriptInteraction('WhitelistVPN', this.name, (targetId, game, token) => {
            if (vpnExceptionIds.includes(targetId)) {
                return;
            }

            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.EntityId = targetId;
            interactionData.Name = 'Whitelist VPN';
            interactionData.DisplayMeta = 'oi-circle-check';

            interactionData.ActionMeta.Add('InteractionId', 'command');
            interactionData.ActionMeta.Add('Data', `whitelistvpn`);
            interactionData.ActionMeta.Add('ActionButtonLabel', 'Allow');
            interactionData.ActionMeta.Add('Name', 'Allow VPN Connection');
            interactionData.ActionMeta.Add('ShouldRefresh', true.toString());

            interactionData.ActionPath = 'DynamicAction';
            interactionData.MinimumPermission = 3;
            interactionData.Source = this.name;
            return interactionData;
        });
    },

    onUnloadAsync: function () {
        this.interactionRegistration.UnregisterInteraction('WhitelistVPN');
    },

    onTickAsync: function (server) {
    }
};
