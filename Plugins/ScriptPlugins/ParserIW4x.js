var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.1,
    name: 'IW3 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser();
        eventParser = manager.GenerateDynamicEventParser();

        rconParser.Configuration.CommandPrefixes.Tell    = 'tellraw {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say     = 'sayraw {0}';
        rconParser.Configuration.CommandPrefixes.Kick    = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban     = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan = 'tempbanclient {0} "{1}"';
        eventParser.Configuration.GameDirectory = 'userraw';

        rconParser.Version = 'IW4x (v0.6.0)';
        eventParser.Version = 'IW4x (v0.6.0)';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};