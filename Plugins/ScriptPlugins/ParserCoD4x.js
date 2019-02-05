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

        rconParser.Configuration.Dvar.Pattern = '^"(.+)" is: "(.+)?" default: "(.+)?" info: "(.+)?"$';
        rconParser.Configuration.Dvar.AddMapping(110, 4);
        rconParser.Version = 'CoD4 X 1.8 win_mingw-x86 build 2055 May  2 2017';

        eventParser.Configuration.GameDirectory = 'main';
        eventParser.Version = 'CoD4 X 1.8 win_mingw-x86 build 2055 May  2 2017';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};