const init = (registerNotify, serviceResolver, config) => {
    registerNotify('IManagementEventSubscriptions.ClientPenaltyAdministered', (penaltyEvent, _) => plugin.onClientPenalty(penaltyEvent));

    plugin.onLoad(serviceResolver, config);
    return plugin;
};

const plugin = {
    author: 'Amos, RaidMax',
    version: '2.1',
    name: 'Broadcast Bans',
    config: null,
    logger: null,
    translations: null,
    manager: null,
    enableBroadcastBans: false,

    onClientPenalty: function (penaltyEvent) {
        if (!this.enableBroadcastBans || penaltyEvent.penalty.type !== 'Ban') {
            return;
        }

        let automatedPenaltyMessage;

        penaltyEvent.penalty.punisher.administeredPenalties?.forEach(penalty => {
            automatedPenaltyMessage = penalty.automatedOffense;
        });

        if (penaltyEvent.penalty.punisher.clientId === 1 && automatedPenaltyMessage !== undefined) {
            let message = this.translations['PLUGINS_BROADCAST_BAN_ACMESSAGE'].replace('{{targetClient}}', penaltyEvent.client.cleanedName);
            this.broadcastMessage(message);
        } else {
            let message = this.translations['PLUGINS_BROADCAST_BAN_MESSAGE'].replace('{{targetClient}}', penaltyEvent.client.cleanedName);
            this.broadcastMessage(message);
        }
    },

    broadcastMessage: function (message) {
        this.manager.getServers().forEach(server => {
            server.broadcast(message);
        });
    },

    onLoad: function (serviceResolver, config) {
        this.config = config;
        this.config.setName(this.name);
        this.enableBroadcastBans = this.config.getValue('EnableBroadcastBans', newConfig => {
            plugin.logger.logInformation('{Name} config reloaded. Enabled={Enabled}', plugin.name, newConfig);
            plugin.enableBroadcastBans = newConfig;
        });

        this.manager = serviceResolver.resolveService('IManager');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.translations = serviceResolver.resolveService('ITranslationLookup');

        if (this.enableBroadcastBans === undefined) {
            this.enableBroadcastBans = false;
            this.config.setValue('EnableBroadcastBans', this.enableBroadcastBans);
        }

        this.logger.logInformation('{Name} {Version} by {Author} loaded. Enabled={Enabled}', this.name, this.version,
            this.author, this.enableBroadcastBans);
    }
};
