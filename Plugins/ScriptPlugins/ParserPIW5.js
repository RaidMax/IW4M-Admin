var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 1.0,
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
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';

        rconParser.Configuration.Dvar.Pattern = '^(.+) is "(.+)?"';
        rconParser.Configuration.Dvar.AddMapping(106, 1);
        rconParser.Configuration.Dvar.AddMapping(107, 2);
        rconParser.Configuration.WaitForResponse = true;
        rconParser.Configuration.CanGenerateLogPath = true;
        rconParser.Configuration.NoticeLineSeparator = '. ';
        rconParser.Configuration.DefaultRConPort = 27016;

        rconParser.Configuration.DefaultInstallationDirectoryHint = '{LocalAppData}/Plutonium/storage/iw5';
                                                        
        rconParser.Configuration.StatusHeader.Pattern = 'num +score +bot +ping +guid +name +address +qport *';
        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +-?([0-9]+) +(0|1) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\\d+\\.\\d+\\.\\d+.\\d+\\:-*\\d{1,5}|0+.0+:-*\\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$';
        rconParser.Configuration.Status.AddMapping(102, 4);
        rconParser.Configuration.Status.AddMapping(103, 5);
        rconParser.Configuration.Status.AddMapping(104, 6);
        // basegame should not contain an absolute directory, but alas...
        rconParser.Configuration.OverrideDvarNameMapping.Add('fs_homepath', 'fs_basegame');

        rconParser.IsOneLog = true;
        rconParser.Version = 'IW5 MP 1.9 build 388110 Fri Sep 14 00:04:28 2012 win-x86';
        rconParser.GameName = 3; // IW5
        eventParser.Version = 'IW5 MP 1.9 build 388110 Fri Sep 14 00:04:28 2012 win-x86';
        eventParser.GameName = 3; // IW5

        eventParser.Configuration.GameDirectory = '';
        eventParser.URLProtocolFormat = 'plutonium://play/iw5mp/{{ip}}:{{port}}';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};