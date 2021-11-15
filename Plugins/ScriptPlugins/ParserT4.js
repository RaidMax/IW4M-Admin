var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.2,
    name: 'Call of Duty 5: World at War Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';
        rconParser.Configuration.GuidNumberStyle = 7; // Integer
        rconParser.Configuration.DefaultRConPort = 28960;
        rconParser.Version = 'Call of Duty Multiplayer COD_WaW MP build 1.7.1263 CL(350073) JADAMS2 Thu Oct 29 15:43:55 2009 win-x86';
        
        eventParser.Configuration.GuidNumberStyle = 7; // Integer
        eventParser.GameName = 5; // T4
        eventParser.Version = 'Call of Duty Multiplayer COD_WaW MP build 1.7.1263 CL(350073) JADAMS2 Thu Oct 29 15:43:55 2009 win-x86';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
