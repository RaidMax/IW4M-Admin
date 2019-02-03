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

        rconParser.Configuration.CommandPrefixes.Tell    = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say     = 'say {0}';
        rconParser.Configuration.CommandPrefixes.Kick    = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban     = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan = 'tempbanclient {0} "{1}"';
        rconParser.Version = 'CoD4 MP 1.8 build 13620 Thu Oct 04 00:43:04 2007 win-x86';

        eventParser.Configuration.GameDirectory = 'main';
        eventParser.Version = 'CoD4 MP 1.8 build 13620 Thu Oct 04 00:43:04 2007 win-x86';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};