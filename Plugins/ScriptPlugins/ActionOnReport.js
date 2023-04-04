const init = (registerEventCallback, serviceResolver, _) => {
    plugin.onLoad(serviceResolver);

    registerEventCallback('IManagementEventSubscriptions.ClientPenaltyAdministered', (penaltyEvent, _) => {
        plugin.onPenalty(penaltyEvent);
    });

    return plugin;
};

const plugin = {
    author: 'RaidMax',
    version: '2.0',
    name: 'Action on Report',
    enabled: false, // indicates if the plugin is enabled
    reportAction: 'TempBan', // can be TempBan or Ban
    maxReportCount: 5, // how many reports before action is taken
    tempBanDurationMinutes: 60, // how long to temporarily ban the player
    penaltyType: {
        'report': 0
    },

    onPenalty: function (penaltyEvent) {
        if (!this.enabled || penaltyEvent.penalty.type !== this.penaltyType['report']) {
            return;
        }

        if (!penaltyEvent.client.isIngame || (penaltyEvent.client.level !== 'User' && penaltyEvent.client.level !== 'Flagged')) {
            this.logger.logInformation(`Ignoring report for client (id) ${penaltyEvent.client.clientId} because they are privileged or not in-game`);
            return;
        }

        let reportCount = this.reportCounts[penaltyEvent.client.networkId] === undefined ? 0 : this.reportCounts[penaltyEvent.Client.NetworkId];
        reportCount++;
        this.reportCounts[penaltyEvent.client.networkId] = reportCount;

        if (reportCount >= this.maxReportCount) {
            switch (this.reportAction) {
                case 'TempBan':
                    this.logger.logInformation(`TempBanning client (id) ${penaltyEvent.client.clientId} because they received ${reportCount} reports`);
                    penaltyEvent.client.tempBan(this.translations['PLUGINS_REPORT_ACTION'], System.TimeSpan.FromMinutes(this.tempBanDurationMinutes), penaltyEvent.Client.CurrentServer.asConsoleClient());
                    break;
                case 'Ban':
                    this.logger.logInformation(`Banning client (id) ${penaltyEvent.client.clientId} because they received ${reportCount} reports`);
                    penaltyEvent.client.ban(this.translations['PLUGINS_REPORT_ACTION'], penaltyEvent.client.currentServer.asConsoleClient(), false);
                    break;
            }
        }
    },

    onLoad: function (serviceResolver) {
        this.translations = serviceResolver.resolveService('ITranslationLookup');
        this.logger = serviceResolver.resolveService('ILogger', ['ScriptPluginV2']);
        this.logger.logInformation('ActionOnReport {version} by {author} loaded. Enabled={enabled}', this.version, this.author, this.enabled);
        this.reportCounts = {};
    }
};
