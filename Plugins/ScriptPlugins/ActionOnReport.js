const init = (registerEventCallback, serviceResolver, configWrapper) => {
    plugin.onLoad(serviceResolver, configWrapper);

    registerEventCallback('IManagementEventSubscriptions.ClientPenaltyAdministered', (penaltyEvent, _) => {
        plugin.onPenalty(penaltyEvent);
    });

    return plugin;
};

const plugin = {
    author: 'RaidMax',
    version: '2.1',
    name: 'Action on Report',
    config: {
        enabled: false, // indicates if the plugin is enabled
        reportAction: 'TempBan', // can be TempBan or Ban
        maxReportCount: 5, // how many reports before action is taken
        tempBanDurationMinutes: 60 // how long to temporarily ban the player
    },

    onPenalty: function (penaltyEvent) {
        if (!this.config.enabled || penaltyEvent.penalty.type !== 'Report') {
            return;
        }
        
        if (!penaltyEvent.client.isIngame || (penaltyEvent.client.level !== 'User' && penaltyEvent.client.level !== 'Flagged')) {
            this.logger.logInformation(`Ignoring report for client (id) ${penaltyEvent.client.clientId} because they are privileged or not in-game`);
            return;
        }

        let reportCount = this.reportCounts[penaltyEvent.client.networkId] === undefined ? 0 : this.reportCounts[penaltyEvent.Client.NetworkId];
        reportCount++;
        this.reportCounts[penaltyEvent.client.networkId] = reportCount;

        if (reportCount >= this.config.maxReportCount) {
            switch (this.config.reportAction) {
                case 'TempBan':
                    this.logger.logInformation(`TempBanning client (id) ${penaltyEvent.client.clientId} because they received ${reportCount} reports`);
                    penaltyEvent.client.tempBan(this.translations['PLUGINS_REPORT_ACTION'], System.TimeSpan.FromMinutes(this.config.tempBanDurationMinutes), penaltyEvent.Client.CurrentServer.asConsoleClient());
                    break;
                case 'Ban':
                    this.logger.logInformation(`Banning client (id) ${penaltyEvent.client.clientId} because they received ${reportCount} reports`);
                    penaltyEvent.client.ban(this.translations['PLUGINS_REPORT_ACTION'], penaltyEvent.client.currentServer.asConsoleClient(), false);
                    break;
            }
        }
    },

    onLoad: function (serviceResolver, configWrapper) {
        this.translations = serviceResolver.resolveService('ITranslationLookup');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.configWrapper = configWrapper;

        const storedConfig = this.configWrapper.getValue('config', newConfig => {
            if (newConfig) {
                plugin.logger.logInformation('ActionOnReport config reloaded. Enabled={Enabled}', newConfig.enabled);
                plugin.config = newConfig;
            }
        });

        if (storedConfig != null) {
            this.config = storedConfig
        } else {
            this.configWrapper.setValue('config', this.config);
        }
        
        this.logger.logInformation('ActionOnReport {version} by {author} loaded. Enabled={Enabled}', this.version, this.author, this.config.enabled);
        this.reportCounts = {};
    }
};
