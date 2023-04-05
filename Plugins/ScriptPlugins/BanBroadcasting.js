const init = (registerNotify, serviceResolver, config) => {
    registerNotify('IManagementEventSubscriptions.ClientPenaltyAdministered', (penaltyEvent, _) => plugin.onClientPenalty(penaltyEvent));

    plugin.onLoad(serviceResolver, config);
    return plugin;
};

const plugin = {
    author: 'Amos, RaidMax',
    version: '2.0',
    name: 'Broadcast Bans',
    config: null,
    logger: null,
    translations: null,
    manager: null,

    onClientPenalty: function (penaltyEvent) {
        if (!this.enableBroadcastBans || penaltyEvent.penalty.type !== 5) {
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
        this.enableBroadcastBans = this.config.getValue('EnableBroadcastBans');

        this.manager = serviceResolver.resolveService('IManager');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.translations = serviceResolver.resolveService('ITranslationLookup');

        if (this.enableBroadcastBans === undefined) {
            this.enableBroadcastBans = false;
            this.config.setValue('EnableBroadcastBans', this.enableBroadcastBans);
        }

        this.logger.logInformation('{Name} {Version} by {Author} loaded. Enabled={enabled}', this.name, this.version,
            this.author, this.enableBroadcastBans);
    }
};
