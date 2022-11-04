var rconParser;
var eventParser;

var plugin = {
    author: 'Diavolo, RaidMax',
    version: 0.3,
    name: 'S1x Parser',
    isParser: true,

    onEventAsync: function(gameEvent, server) {},

    onLoadAsync: function(manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        rconParser.Configuration.CommandPrefixes.Kick = 'kickClient {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban = 'kickClient {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan = 'kickClient {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint';
        rconParser.Configuration.Dvar.Pattern = '^ *\\"(.+)\\" is: \\"(.+)?\\" default: \\"(.+)?\\"\\n?(?:latched: \\"(.+)?\\"\\n?)? *(.+)$';
        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +-?([0-9]+) +(Yes|No) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\\d+\\.\\d+\\.\\d+.\\d+\\:-*\\d{1,5}|0+.0+:-*\\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$';
        rconParser.Configuration.StatusHeader.Pattern = 'num +score +bot +ping +guid +name +address +qport *';
        rconParser.Configuration.Status.AddMapping(102, 4);
        rconParser.Configuration.Status.AddMapping(103, 5);
        rconParser.Configuration.Status.AddMapping(104, 6);
        rconParser.Configuration.WaitForResponse = false;
        rconParser.Configuration.DefaultRConPort = 27016;

        eventParser.Configuration.GameDirectory = '';
        eventParser.Configuration.LocalizeText = '\x1f';

        rconParser.Version = 'S1 MP 1.22 build 2195988 Wed Apr 18 11:26:14 2018 win64';
        rconParser.GameName = 9; // SHG1
        eventParser.Version = 'S1 MP 1.22 build 2195988 Wed Apr 18 11:26:14 2018 win64';
        eventParser.GameName = 9; // SHG1
    },

    onUnloadAsync: function() {},

    onTickAsync: function(server) {}
};
