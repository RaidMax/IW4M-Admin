let plugin = {
    author: 'RaidMax',
    version: 1.0,
    name: 'Action on Report',
    enabled: false, // indicates if the plugin is enabled
    reportAction: 'TempBan', // can be TempBan or Ban
    maxReportCount: 5, // how many reports before action is taken
    tempBanDurationMinutes: 60, // how long to temporarily ban the player
    eventTypes: { 'report': 103 },

    onEventAsync: function (gameEvent, server) {
        if (!this.enabled) {
            return;
        }

        if (gameEvent.Type === this.eventTypes['report']) {
            if (!gameEvent.Target.IsIngame) {
                return;
            }

            let reportCount = this.reportCounts[gameEvent.Target.NetworkId] === undefined ? 0 : this.reportCounts[gameEvent.Target.NetworkId];
            reportCount++;
            this.reportCounts[gameEvent.Target.NetworkId] = reportCount;

            if (reportCount >= this.maxReportCount) {
                switch (this.reportAction) {
                    case 'TempBan':
                        server.Logger.WriteInfo(`TempBanning client (id) ${gameEvent.Target.ClientId} because they received ${reportCount} reports`);
                        gameEvent.Target.TempBan(_localization.LocalizationIndex['PLUGINS_REPORT_ACTION'], System.TimeSpan.FromMinutes(this.tempBanDurationMinutes), _IW4MAdminClient);
                        break;
                    case 'Ban':
                        server.Logger.WriteInfo(`Banning client (id) ${gameEvent.Target.ClientId} because they received ${reportCount} reports`);
                        gameEvent.Target.Ban(_localization.LocalizationIndex['PLUGINS_REPORT_ACTION'], _IW4MAdminClient, false);
                        break;
                }
            }
        }
    },

    onLoadAsync: function (manager) {
        this.reportCounts = {};
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};