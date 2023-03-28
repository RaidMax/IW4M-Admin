var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax, Diamante',
    version: 0.7,
    name: 'IW4x Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        rconParser.Configuration.CommandPrefixes.Tell    = 'tellraw {0} {1}';
        rconParser.Configuration.CommandPrefixes.Say     = 'sayraw {0}';
        rconParser.Configuration.CommandPrefixes.Kick    = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban     = 'clientkick {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan = 'clientkick {0} "{1}"';
        
        rconParser.Configuration.DefaultRConPort = 28960;
        rconParser.Configuration.DefaultInstallationDirectoryHint = 'HKEY_CURRENT_USER\\Software\\Classes\\iw4x\\shell\\open\\command';
        rconParser.Configuration.FloodProtectInterval = 150;

        eventParser.Configuration.GameDirectory = 'userraw';

        rconParser.Version = 'IW4x MP (Beta) build 0.7.8 latest Mar 17 2023 18:30:03 win-x86';
        rconParser.GameName = 2; // IW4x
        eventParser.Version = 'IW4x MP (Beta) build 0.7.8 latest Mar 17 2023 18:30:03 win-x86';
        eventParser.GameName = 2; // IW4x
        eventParser.URLProtocolFormat = 'iw4x://{{ip}}:{{port}}';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
