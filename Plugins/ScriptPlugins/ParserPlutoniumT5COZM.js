var rconParser;
var eventParser;

var plugin = {
    author: 'RaidMax, SwordSWD',
    version: 0.2,
    name: 'Plutonium T5 CO-OP/Zombies Parser',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser = manager.GenerateDynamicRConParser(this.name);
        eventParser = manager.GenerateDynamicEventParser(this.name);

        rconParser.Configuration.DefaultInstallationDirectoryHint = '{LocalAppData}/Plutonium/storage/t5';
        rconParser.Configuration.CommandPrefixes.RConResponse = '\xff\xff\xff\xffprint\n';
        rconParser.Configuration.Dvar.Pattern = '^(?:\\^7)?\\"(.+)\\" is: \\"(.+)?\\" default: \\"(.+)?\\"\\n?(?:latched: \\"(.+)?\\"\\n)?\\w*(.+)*$';
        rconParser.Configuration.CommandPrefixes.Tell = 'tell {0} {1}';
        rconParser.Configuration.CommandPrefixes.RConGetInfo = undefined;
        rconParser.Configuration.GuidNumberStyle = 7; // Integer
        rconParser.Configuration.DefaultRConPort = 3074;
        rconParser.Configuration.CanGenerateLogPath = false;
        rconParser.Configuration.CommandPrefixes.Kick         = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.Ban          = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.TempBan      = 'clientkick {0}';
        rconParser.Configuration.CommandPrefixes.RConGetInfo  = undefined;

        rconParser.Configuration.OverrideCommandTimeouts.Clear();
        rconParser.Configuration.OverrideCommandTimeouts.Add('map', 0);
        rconParser.Configuration.OverrideCommandTimeouts.Add('map_rotate', 0);
        rconParser.Configuration.OverrideCommandTimeouts.Add('fast_restart', 0);

        rconParser.Version = 'Call of Duty Singleplayer - Ship COD_T5_S SP build 7.0.189 CL(966072) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86';
        rconParser.GameName = 6; //  T5
        eventParser.Version = 'Call of Duty Singleplayer - Ship COD_T5_S SP build 7.0.189 CL(966072) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86';
        eventParser.GameName = 6; // T5
        eventParser.Configuration.GuidNumberStyle = 7; // Integer
        eventParser.Configuration.GameDirectory   = 'main';

    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};
