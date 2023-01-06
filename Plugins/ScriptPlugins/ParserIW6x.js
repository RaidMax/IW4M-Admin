var rconParser;
var eventParser;

var plugin = {
    author: 'Xerxes, RaidMax, st0rm',
    version: 0.5,
    name: 'IW6x Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        rconParser.Configuration.CommandPrefixes.Tell = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say = 'say {0}';
        rconParser.Configuration.CommandPrefixes.Kick = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';
        rconParser.Configuration.Dvar.Pattern = '^ *\\"(.+)\\" is: \\"(.+)?\\" default: \\"(.+)?\\"\\n?(?:latched: \\"(.+)?\\"\\n?)?(.*)$';
        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +-?([0-9]+) +(Yes|No) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\\d+\\.\\d+\\.\\d+.\\d+\\:-*\\d{1,5}|0+.0+:-*\\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$';
        rconParser.Configuration.StatusHeader.Pattern = 'num +score +bot +ping +guid +name +address +qport *';
        rconParser.Configuration.WaitForResponse = false;
        rconParser.Configuration.DefaultRConPort = 28960;
        
        rconParser.Configuration.Status.AddMapping(102, 4);
        rconParser.Configuration.Status.AddMapping(103, 5);
        rconParser.Configuration.Status.AddMapping(104, 6);

        rconParser.Version = 'IW6 MP 3.15 build 2 Sat Sep 14 2013 03:58:30PM win64';
        rconParser.GameName = 4; // IW6
        eventParser.Version = 'IW6 MP 3.15 build 2 Sat Sep 14 2013 03:58:30PM win64';
        eventParser.GameName = 4; // IW6
        
        eventParser.Configuration.GameDirectory = '';
        eventParser.Configuration.LocalizeText = '\x1f';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
