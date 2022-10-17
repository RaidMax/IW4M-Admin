let vpnExceptionIds = [];
const vpnAllowListKey = 'Webfront::Nav::Admin::VPNAllowList';
const vpnWhitelistKey = 'Webfront::Profile::VPNWhitelist';

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
},
{
    name: 'disallowvpn',
    description: 'disallows a player from connecting with a VPN',
    alias: 'dv',
    permission: 'SeniorAdmin',
    targetRequired: true,
    arguments: [{
        name: 'player',
        required: true
    }],
    execute: (gameEvent) => {
        vpnExceptionIds = vpnExceptionIds.filter(exception => parseInt(exception) !== parseInt(gameEvent.Target.ClientId));
        plugin.configHandler.SetValue('vpnExceptionIds', vpnExceptionIds);

        gameEvent.Origin.Tell(`Successfully disallowed ${gameEvent.Target.Name} from connecting with VPN`);
    }
}];

const getClientsData = (clientIds) => {
    const contextFactory = _serviceResolver.ResolveService('IDatabaseContextFactory');
    const context = contextFactory.CreateContext(false);
    const clientSet = context.Clients;
    const clients = clientSet.GetClientsBasicData(clientIds);
    context.Dispose();

    return clients;
}

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
            if (parseInt(id) === parseInt(origin.ClientId)) {
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

        // registers the profile action
        this.interactionRegistration.RegisterScriptInteraction(vpnWhitelistKey, this.name, (targetId, game, token) => {
            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();

            interactionData.ActionPath = 'DynamicAction';
            interactionData.InteractionId = vpnWhitelistKey;
            interactionData.EntityId = targetId;
            interactionData.MinimumPermission = 3;
            interactionData.Source = this.name;
            interactionData.ActionMeta.Add('InteractionId', 'command'); // indicate we're wanting to execute a command
            interactionData.ActionMeta.Add('ShouldRefresh', true.toString()); // indicates that the page should refresh after performing the action

            if (vpnExceptionIds.includes(targetId)) {
                interactionData.Name = _localization.LocalizationIndex['WEBFRONT_VPN_BUTTON_DISALLOW']; // text for the profile button
                interactionData.DisplayMeta = 'oi-circle-x';

                interactionData.ActionMeta.Add('Data', `disallowvpn`);         // command to execute
                interactionData.ActionMeta.Add('ActionButtonLabel', _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_DISALLOW_CONFIRM']);   // confirm button on the dialog
                interactionData.ActionMeta.Add('Name', _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_DISALLOW_TITLE']);                 // title on the confirm dialog
            } else {
                interactionData.Name = _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_ALLOW']; // text for the profile button
                interactionData.DisplayMeta = 'oi-circle-check';
                
                interactionData.ActionMeta.Add('Data', `whitelistvpn`);         // command to execute
                interactionData.ActionMeta.Add('ActionButtonLabel', _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_ALLOW_CONFIRM']);   // confirm button on the dialog
                interactionData.ActionMeta.Add('Name', _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_ALLOW_TITLE']);                 // title on the confirm dialog
            }

            return interactionData;
        });

        // registers the navigation/page
        this.interactionRegistration.RegisterScriptInteraction(vpnAllowListKey, this.name, (targetId, game, token) => {

            const helpers = importNamespace('SharedLibraryCore.Helpers');
            const interactionData = new helpers.InteractionData();
            
            interactionData.Name = _localization.LocalizationIndex['WEBFRONT_NAV_VPN_TITLE'];         // navigation link name
            interactionData.Description =  _localization.LocalizationIndex['WEBFRONT_NAV_VPN_DESC'];  // alt and title
            interactionData.DisplayMeta = 'oi-circle-check'; // nav icon
            interactionData.InteractionId = vpnAllowListKey;
            interactionData.MinimumPermission = 3; // moderator
            interactionData.InteractionType = 2; // 1 is RawContent for apis etc..., 2 is 
            interactionData.Source = this.name;

            interactionData.ScriptAction = (sourceId, targetId, game, meta, token) => {
                const clientsData = getClientsData(vpnExceptionIds);

                let table = '<table class="table bg-dark-dm bg-light-lm">';

                const disallowInteraction = {
                    InteractionId: 'command',
                    Data: 'disallowvpn',
                    ActionButtonLabel: _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_DISALLOW_CONFIRM'],
                    Name: _localization.LocalizationIndex['WEBFRONT_VPN_ACTION_DISALLOW_TITLE']
                };

                if (clientsData.length === 0)
                {
                    table += `<tr><td>No players are whitelisted.</td></tr>`
                }

                clientsData.forEach(client => {
                    table +=    `<tr>
                                    <td>
                                        <a href="/Client/Profile/${client.ClientId}" class="level-color-${client.Level.toLowerCase()} no-decoration">${client.CurrentAlias.Name.StripColors()}</a>
                                    </td>
                                    <td>
                                        <a href="#" class="profile-action no-decoration float-right" data-action="DynamicAction" data-action-id="${client.ClientId}"
                                           data-action-meta="${encodeURI(JSON.stringify(disallowInteraction))}">
                                            <div class="btn">
                                                <i class="oi oi-circle-x mr-5 font-size-12"></i>
                                                <span class="text-truncate">${_localization.LocalizationIndex['WEBFRONT_VPN_BUTTON_DISALLOW']}</span>
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

    onUnloadAsync: function () {
        this.interactionRegistration.UnregisterInteraction(vpnWhitelistKey);
        this.interactionRegistration.UnregisterInteraction(vpnAllowListKey);
    },

    onTickAsync: function (server) {
    }
};
