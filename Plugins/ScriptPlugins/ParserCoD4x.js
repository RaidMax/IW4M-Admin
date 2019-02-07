var rconParser;
var eventParser;

var plugin = {
    author: 'FrenchFry, RaidMax',
    version: 0.2,
    name: 'CoD4x Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser();
        eventParser = manager.GenerateDynamicEventParser();

        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){16}|(?:[a-z]|[0-9]){32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +([0-9]+) +(\\d+\\.\\d+\\.\\d+.\\d+\\:-*\\d{1,5}|0+.0+:-*\\d{1,5}|loopback) +(-*[0-9]+) +([0-9]+) *$'
        rconParser.Configuration.Status.AddMapping(104, 6); // RConName
        rconParser.Configuration.Status.AddMapping(105, 8); // RConIPAddress

        rconParser.Configuration.Dvar.Pattern = '^"(.+)" is: "(.+)?" default: "(.+)?" info: "(.+)?"$';
        rconParser.Configuration.Dvar.AddMapping(109, 2); // DVAR latched value
        rconParser.Configuration.Dvar.AddMapping(110, 4); // dvar info
        rconParser.Version = 'CoD4 X 1.8 win_mingw-x86 build 2055 May  2 2017';
        rconParser.GameName = 1; // IW3

        eventParser.Configuration.GameDirectory = 'main';
        eventParser.Version = 'CoD4 X 1.8 win_mingw-x86 build 2055 May  2 2017';
        eventParser.GameName = 1; // IW3
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};