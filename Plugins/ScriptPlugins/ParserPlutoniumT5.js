var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax, diamante0018, ineedbots',
    version: 0.3,
    name: 'Plutonium T5 Parser (2025)',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);
        eventParser.Configuration.GameDirectory = 'main';

        rconParser.Configuration.DefaultInstallationDirectoryHint = '{LocalAppData}/Plutonium/storage/t5';
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';
        rconParser.Configuration.Dvar.Pattern = '^(?:\\^7)?\\"(.+)\\" is: \\"(.+)?\\" default: \\"(.+)?\\"\\n?(?:latched: \\"(.+)?\\"\\n)?\\w*(.+)*$';
        rconParser.Configuration.CommandPrefixes.Tell = 'tell {0} {1}';
        rconParser.Configuration.GuidNumberStyle = 7; // Integer
        rconParser.Configuration.DefaultRConPort = 28960;

        rconParser.Version = 'Call of Duty Multiplayer - Ship COD_T5_S MP build 7.0.189 CL(1022875) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86';
        rconParser.GameName = 6; //  T5
        eventParser.Version = 'Call of Duty Multiplayer - Ship COD_T5_S MP build 7.0.189 CL(1022875) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86';
        eventParser.GameName = 6; // T5
        eventParser.Configuration.GuidNumberStyle = 7; // Integer

    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
