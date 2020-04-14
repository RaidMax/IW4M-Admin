var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.1,
    name: 'Plutonium IW5 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

       rconParser.Configuration.CommandPrefixes.Tell        = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say         = 'say {0}';
        rconParser.Configuration.CommandPrefixes.Kick        = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban         = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan     = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.RConGetDvar = '\xff\xff\xff\xffrcon {0} get {1}';

        rconParser.Configuration.Dvar.Pattern = '^(.+) is "(.+)?"';
        rconParser.Configuration.Dvar.AddMapping(106, 1);
        rconParser.Configuration.Dvar.AddMapping(107, 2);
        rconParser.Configuration.WaitForResponse = false;
        rconParser.Configuration.CanGenerateLogPath = true;

        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +([0-9]+) +(?:[0-1]{1}) +([0-9]+) +([A-F0-9]+) +(.+?) +(?:[0-9]+) +(\\d+\\.\\d+\\.\\d+\\.\\d+\\:-?\\d{1,5}|0+\\.0+:-?\\d{1,5}|loopback) +(?:-?[0-9]+) +(?:[0-9]+) *$';
        rconParser.Configuration.Status.AddMapping(100, 1);
        rconParser.Configuration.Status.AddMapping(101, 2);
        rconParser.Configuration.Status.AddMapping(102, 3);
        rconParser.Configuration.Status.AddMapping(103, 4);
        rconParser.Configuration.Status.AddMapping(104, 5);
        rconParser.Configuration.Status.AddMapping(105, 6);

        rconParser.Version = 'IW5 MP 1.9 build 388110 Fri Sep 14 00:04:28 2012 win-x86';
        rconParser.GameName = 3; // IW5
        eventParser.Version = 'IW5 MP 1.9 build 388110 Fri Sep 14 00:04:28 2012 win-x86';
        eventParser.GameName = 3; // IW5

        eventParser.Configuration.GameDirectory = '';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};