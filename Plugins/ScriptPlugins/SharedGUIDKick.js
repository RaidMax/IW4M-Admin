var plugin = {
    author: 'RaidMax',
    version: 1.1,
    name: 'Shared GUID Kicker Plugin',

    onEventAsync: function (gameEvent, server) {
        // make sure we only check for IW4(x)
        if (server.GameName !== 2) {
            return false;
        }

        // connect or join event
        if (gameEvent.Type === 3) {
            // this GUID seems to have been packed in a IW4 torrent and results in an unreasonable amount of people using the same GUID
            if (gameEvent.Origin.NetworkId === -805366929435212061 ||
                gameEvent.Origin.NetworkId === 3150799945255696069 ||
                gameEvent.Origin.NetworkId === 5859032128210324569 ||
                gameEvent.Origin.NetworkId === 2908745942105435771 ||
                gameEvent.Origin.NetworkId === -6492697076432899192 ||
                gameEvent.Origin.NetworkId === 1145760003260769995 ||
                gameEvent.Origin.NetworkId === -7102887284306116957 ||
                gameEvent.Origin.NetworkId === 3304388024725980231) {
                gameEvent.Origin.Kick('Your GUID is generic. Delete players/guids.dat and rejoin', _IW4MAdminClient);
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