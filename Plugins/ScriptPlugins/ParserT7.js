var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax',
    version: 0.3,
    name: 'Black Ops 3 Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        rconParser.Configuration.Status.Pattern = '^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\\d+\\.\\d+\\.\\d+.\\d+\\:-*\\d{1,5}|0+.0+:-*\\d{1,5}|loopback|unknown)(?:\\([0-9]+\\)) +(-*[0-9]+) *$';
        rconParser.Configuration.StatusHeader.Pattern           = 'num +score +ping +xuid +name +address +qport';
        rconParser.Configuration.CommandPrefixes.Kick           = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.Ban            = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.TempBan        = 'tempbanclient {0}';
        rconParser.Configuration.CommandPrefixes.RConCommand    = '\xff\xff\xff\xff\x00{0} {1}';
        rconParser.Configuration.CommandPrefixes.RConGetDvar    = '\xff\xff\xff\xff\x00{0} {1}';
        rconParser.Configuration.CommandPrefixes.RConSetDvar    = '\xff\xff\xff\xff\x00{0} set {1}';
        rconParser.Configuration.CommandPrefixes.RConResponse   = '\xff\xff\xff\xff\x01';
        rconParser.Configuration.GametypeStatus.Pattern         = 'Gametype: (.+)';
        rconParser.Configuration.MapStatus.Pattern              = 'Map: (.+)';
        rconParser.Configuration.CommandPrefixes.RConGetInfo    = undefined; // disables this, because it's useless on T7
        rconParser.Configuration.ServerNotRunningResponse       = 'this is here to prevent a hibernating server from being detected as not running';

        rconParser.Configuration.OverrideDvarNameMapping.Add('sv_hostname', 'live_steam_server_name');
        rconParser.Configuration.DefaultDvarValues.Add('sv_running', '1');
        rconParser.Configuration.DefaultDvarValues.Add('g_gametype', '');
        rconParser.Configuration.DefaultDvarValues.Add('fs_basepath', '');
        rconParser.Configuration.DefaultDvarValues.Add('fs_basegame', '');
        rconParser.Configuration.DefaultDvarValues.Add('fs_game', '');

        rconParser.Configuration.Status.AddMapping(105, 6); // ip address
        rconParser.Configuration.GametypeStatus.AddMapping(112, 1); // gametype
        rconParser.Version = '[local] ship win64 CODBUILD8-764 (3421987) Mon Dec 16 10:44:20 2019 10d27bef';
        rconParser.GameName = 8; // BO3
        rconParser.CanGenerateLogPath = false;

        eventParser.Version = '[local] ship win64 CODBUILD8-764 (3421987) Mon Dec 16 10:44:20 2019 10d27bef';
        eventParser.GameName = 8; // BO3
        eventParser.Configuration.GameDirectory = 'usermaps';
        eventParser.Configuration.Say.Pattern = '^(chat|chatteam);(?:[0-9]+);([0-9]+);([0-9]+);(.+);(.*)$';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
