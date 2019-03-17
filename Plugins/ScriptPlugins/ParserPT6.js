var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.2,
    name: 'Plutonium T6 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser();
        eventParser = manager.GenerateDynamicEventParser();

        rconParser.Configuration.CommandPrefixes.Tell        = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say         = 'say {0}';
        rconParser.Configuration.CommandPrefixes.Kick        = 'clientkick_for_reason {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban         = 'clientkick_for_reason {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan     = 'clientkick_for_reason {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.RConGetDvar = '\xff\xff\xff\xffrcon {0} get {1}';

        rconParser.Configuration.Dvar.Pattern = '^(.+) is "(.+)?"$';
        rconParser.Configuration.Dvar.AddMapping(106, 1);
        rconParser.Configuration.Dvar.AddMapping(107, 2);
        rconParser.Configuration.WaitForResponse = false;

        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +([0-9]+) +(.+) +((?:[A-Z]+|[0-9]+)) +((?:[A-Z]|[0-9]){8,16}) +(.{0,16}) +([0-9]+) +(\\d+\\.\\d+\\.\\d+\\.\\d+\\:-?\\d{1,5}|0+\\.0+:-?\\d{1,5}|loopback) +(-?[0-9]+) +([0-9]+) *$'
        rconParser.Configuration.Status.AddMapping(100, 1);
        rconParser.Configuration.Status.AddMapping(101, 2);
        rconParser.Configuration.Status.AddMapping(102, 4);
        rconParser.Configuration.Status.AddMapping(103, 5);
        rconParser.Configuration.Status.AddMapping(104, 6);
        rconParser.Configuration.Status.AddMapping(105, 8);

        eventParser.Configuration.GameDirectory = 't6r\\data';

        rconParser.Version = 'Call of Duty Multiplayer - Ship COD_T6_S MP build 1.0.44 CL(1759941) CODPCAB2 CEG Fri May 9 19:19:19 2014 win-x86 813e66d5';
        rconParser.GameName = 7; // T6
        eventParser.Version = 'Call of Duty Multiplayer - Ship COD_T6_S MP build 1.0.44 CL(1759941) CODPCAB2 CEG Fri May 9 19:19:19 2014 win-x86 813e66d5';
        eventParser.GameName = 7; // T6
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};