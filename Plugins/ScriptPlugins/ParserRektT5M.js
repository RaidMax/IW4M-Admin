var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.3,
    name: 'RektT5m Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        eventParser.Configuration.GameDirectory = 'data';

        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xff\x01print\n';
        rconParser.Configuration.CommandPrefixes.Tell = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.RConGetInfo = undefined;

        rconParser.Version = 'Call of Duty Multiplayer - Ship COD_T5_S MP build 7.0.189 CL(1022875) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86';
        rconParser.GameName = 6; //  T5
        eventParser.Version = 'Call of Duty Multiplayer - Ship COD_T5_S MP build 7.0.189 CL(1022875) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86';
        eventParser.GameName = 6; // T5
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};