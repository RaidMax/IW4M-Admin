var plugin = {
    author: 'RaidMax',
    version: 1.0,
    name: 'Shared GUID Kicker Plugin',

    onEventAsync: function (gameEvent, server) {
        // connect event
        if (gameEvent.Type === 3 ||
               gameEvent.Type === 4) {
            if (gameEvent.Origin.NetworkId === -805366929435212061) {
                gameEvent.Origin.Kick('Your GUID is generic. Delete players/guids.dat and rejoin', _utilities.IW4MAdminClient);
            }
        }
    },

    onLoadAsync: function (manager) {
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};