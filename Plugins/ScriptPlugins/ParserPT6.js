var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax, Xerxes',
    version: 0.7,
    name: 'Plutonium T6 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

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

        rconParser.Configuration.StatusHeader.Patter = 'num +score +bot +ping +guid +name +lastmsg +address +qport +rate *';
        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +([0-9]+) +(?:[0-1]{1}) +([0-9]+) +([A-F0-9]+) +(.+?) +(?:[0-9]+) +(\\d+\\.\\d+\\.\\d+\\.\\d+\\:-?\\d{1,5}|0+\\.0+:-?\\d{1,5}|loopback) +(?:-?[0-9]+) +(?:[0-9]+) *$';
        rconParser.Configuration.Status.AddMapping(100, 1);
        rconParser.Configuration.Status.AddMapping(101, 2);
        rconParser.Configuration.Status.AddMapping(102, 3);
        rconParser.Configuration.Status.AddMapping(103, 4);
        rconParser.Configuration.Status.AddMapping(104, 5);
        rconParser.Configuration.Status.AddMapping(105, 6);
        
        eventParser.Configuration.GameDirectory = 't6r\\data';
        eventParser.Configuration.GuidNumberStyle = 7; // Integer

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