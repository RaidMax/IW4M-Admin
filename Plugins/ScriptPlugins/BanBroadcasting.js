const broadcastMessage = (server, message) => {
    server.Manager.GetServers().forEach(s => {
        s.Broadcast(message);
    });
};

const plugin = {
    author: 'Amos',
    version: 1.0,
    name: 'Broadcast Bans',

    onEventAsync: function (gameEvent, server) {
        if (!this.enableBroadcastBans) {
            return;
        }

        if (gameEvent.TypeName === 'Ban') {
            let penalty = undefined;
            gameEvent.Origin.AdministeredPenalties?.forEach(p => {
                penalty = p.AutomatedOffense;
            })

            if (gameEvent.Origin.ClientId === 1 && penalty !== undefined) {
                let localization = _localization.LocalizationIndex['PLUGINS_BROADCAST_BAN_ACMESSAGE'].replace('{{targetClient}}', gameEvent.Target.CleanedName);
                broadcastMessage(server, localization);
            } else {
                let localization = _localization.LocalizationIndex['PLUGINS_BROADCAST_BAN_MESSAGE'].replace('{{targetClient}}', gameEvent.Target.CleanedName);
                broadcastMessage(server, localization);
            }
        }
    },

    onLoadAsync: function (manager) {
        this.configHandler = _configHandler;
        this.enableBroadcastBans = this.configHandler.GetValue('EnableBroadcastBans');

        if (this.enableBroadcastBans === undefined) {
            this.enableBroadcastBans = false;
            this.configHandler.SetValue('EnableBroadcastBans', this.enableBroadcastBans);
        }
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
