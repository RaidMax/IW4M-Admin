var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.1,
    name: 'Tekno MW3 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser();
        eventParser = manager.GenerateDynamicEventParser();

        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[A-Z]|[0-9]){16,32})\t +(.{0,16}) +([0-9]+) +(\\d+\\.\\d+\\.\\d+\\.\\d+\\:-?\\d{1,5}|0+\\.0+\\:-?\\d{1,5}|loopback) *$';
        rconParser.Configuration.Status.AddMapping(104, 5); // RConName
        rconParser.Configuration.Status.AddMapping(103, 4); // RConNetworkId
        rconParser.Configuration.CommandPrefixes.RConGetInfo = undefined;
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xff';
        rconParser.Configuration.CommandPrefixes.Tell = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say = 'say {0}';
        rconParser.Configuration.CommandPrefixes.Kick = 'dropclient {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban = 'dropclient {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan = 'tempbanclient {0} "{1}"';
        rconParser.Configuration.Dvar.AddMapping(107, 1); // RCon DvarValue
        rconParser.Configuration.Dvar.Pattern = '^(.*)$';
        rconParser.Version = 'IW5 MP 1.4 build 382 latest Thu Jan 19 2012 11:09:49AM win-x86';

        eventParser.Configuration.GameDirectory = 'scripts';
        eventParser.Version = 'IW5 MP 1.4 build 382 latest Thu Jan 19 2012 11:09:49AM win-x86';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};