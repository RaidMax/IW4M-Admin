var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.6,
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
        rconParser.Configuration.CommandPrefixes.TempBan = 'tempbanclient {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Mute    = 'muteClient {0}';
        rconParser.Configuration.CommandPrefixes.Unmute  = 'unmute {0}';
        
        rconParser.Configuration.DefaultRConPort = 28960;
        rconParser.Configuration.DefaultInstallationDirectoryHint = 'HKEY_CURRENT_USER\\Software\\Classes\\iw4x\\shell\\open\\command';
        rconParser.Configuration.FloodProtectInterval = 150;

        eventParser.Configuration.GameDirectory = 'userraw';

        rconParser.Version = 'IW4x (v0.6.0)';
        rconParser.GameName = 2; // IW4x
        eventParser.Version = 'IW4x (v0.6.0)';
        eventParser.GameName = 2; // IW4x
        eventParser.URLProtocolFormat = 'iw4x://{{ip}}:{{port}}';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
