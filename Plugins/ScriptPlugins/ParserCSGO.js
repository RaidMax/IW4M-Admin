let rconParser;
let eventParser;

const plugin = {
    author: 'RaidMax',
    version: 0.3,
    name: 'CS:GO Parser',
    engine: 'Source',
    isParser: true,

    onEventAsync: function (gameEvent, server) {
    },

    onLoadAsync: function (manager) {
        rconParser              = manager.GenerateDynamicRConParser(this.name);
        eventParser             = manager.GenerateDynamicEventParser(this.name);
        rconParser.RConEngine   = this.engine;
        
        rconParser.Configuration.StatusHeader.Pattern = 'userid +name +uniqueid +connected +ping +loss +state +rate +adr';
        
        rconParser.Configuration.MapStatus.Pattern = '^map *: +(.+)$';
        rconParser.Configuration.MapStatus.AddMapping(111, 1);
        
        rconParser.Configuration.HostnameStatus.Pattern = '^hostname: +(.+)$';
        rconParser.Configuration.MapStatus.AddMapping(113, 1);

        rconParser.Configuration.MaxPlayersStatus.Pattern = '^players *: +\\d+ humans, \\d+ bots \\((\\d+).+';
        rconParser.Configuration.MapStatus.AddMapping(114, 1);

        rconParser.Configuration.Dvar.Pattern = '^"(.+)" = "(.+)" (?:\\( def. "(.*)" \\))?(?: |\\w)+- (.+)$';
        rconParser.Configuration.Dvar.AddMapping(106, 1);
        rconParser.Configuration.Dvar.AddMapping(107, 2);
        rconParser.Configuration.Dvar.AddMapping(108, 3);
        rconParser.Configuration.Dvar.AddMapping(109, 3);

        rconParser.Configuration.Status.Pattern = '^#\\s*(\\d+) (\\d+) "(.+)" (\\S+) +(\\d+:\\d+(?::\\d+)?) (\\d+) (\\S+) (\\S+) (\\d+) (\\d+\\.\\d+\\.\\d+\\.\\d+:\\d+)$';
        rconParser.Configuration.Status.AddMapping(100, 2);
        rconParser.Configuration.Status.AddMapping(101, -1);
        rconParser.Configuration.Status.AddMapping(102, 6);
        rconParser.Configuration.Status.AddMapping(103, 4)
        rconParser.Configuration.Status.AddMapping(104, 3);
        rconParser.Configuration.Status.AddMapping(105, 10);
        rconParser.Configuration.Status.AddMapping(200, 1);
        
        rconParser.Configuration.DefaultDvarValues.Add('sv_running', '1');
        rconParser.Configuration.DefaultDvarValues.Add('version', this.engine);
        rconParser.Configuration.DefaultDvarValues.Add('fs_basepath', '');
        rconParser.Configuration.DefaultDvarValues.Add('fs_basegame', '');
        rconParser.Configuration.DefaultDvarValues.Add('g_log', '');
        rconParser.Configuration.DefaultDvarValues.Add('net_ip', 'localhost');
        
        rconParser.Configuration.OverrideDvarNameMapping.Add('sv_hostname', 'hostname');
        rconParser.Configuration.OverrideDvarNameMapping.Add('mapname', 'host_map');
        rconParser.Configuration.OverrideDvarNameMapping.Add('sv_maxclients', 'maxplayers');
        rconParser.Configuration.OverrideDvarNameMapping.Add('g_gametype', 'game_type'); // todo: will need gamemode too
        rconParser.Configuration.OverrideDvarNameMapping.Add('fs_game', 'game_mode');
        rconParser.Configuration.OverrideDvarNameMapping.Add('g_password', 'sv_password');

        rconParser.Configuration.NoticeLineSeparator = '. ';
        rconParser.CanGenerateLogPath = false;

        rconParser.Configuration.CommandPrefixes.RConGetInfo    = undefined;
        rconParser.Configuration.CommandPrefixes.Kick           = 'kickid {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Ban            = 'kickid {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.TempBan        = 'kickid {0} "{1}"';
        rconParser.Configuration.CommandPrefixes.Say            = 'say {0}';
        rconParser.Configuration.CommandPrefixes.Tell           = 'say [{0}] {1}'; // no tell exists in vanilla
        
        eventParser.Configuration.Say.Pattern = '^"(.+)<(\\d+)><(.+)><(.*?)>" (?:say|say_team) "(.*)"$';
        eventParser.Configuration.Say.AddMapping(5, 1);
        eventParser.Configuration.Say.AddMapping(3, 2);
        eventParser.Configuration.Say.AddMapping(1, 3);
        eventParser.Configuration.Say.AddMapping(7, 4);
        eventParser.Configuration.Say.AddMapping(13, 5);
        
        eventParser.Configuration.Kill.Pattern = '^"(.+)<(\\d+)><(.+)><(.*)>" \\[-?\\d+ -?\\d+ -?\\d+\\] killed "(.+)<(\\d+)><(.+)><(.*)>" \\[-?\\d+ -?\\d+ -?\\d+\\] with "(\\S*)" *(?:\\((\\w+)((?: ).+)?\\))?$';
        eventParser.Configuration.Kill.AddMapping(5, 1);
        eventParser.Configuration.Kill.AddMapping(3, 2);
        eventParser.Configuration.Kill.AddMapping(1, 3);
        eventParser.Configuration.Kill.AddMapping(7, 4);
        eventParser.Configuration.Kill.AddMapping(6, 5);
        eventParser.Configuration.Kill.AddMapping(4, 6);
        eventParser.Configuration.Kill.AddMapping(2, 7);
        eventParser.Configuration.Kill.AddMapping(8, 8);
        eventParser.Configuration.Kill.AddMapping(9, 9);
        eventParser.Configuration.Kill.AddMapping(11, 11);
        
        eventParser.Configuration.Time.Pattern = '^L [01]\\d/[0-3]\\d/\\d+ - [0-2]\\d:[0-5]\\d:[0-5]\\d:';

        rconParser.Version      = 'CSGO';
        rconParser.GameName     = 10; // CSGO
        eventParser.Version     = 'CSGO';
        eventParser.GameName    = 10; // CSGO
        eventParser.URLProtocolFormat = 'steam://connect/{{ip}}:{{port}}';
    },

    onUnloadAsync: function () {
    },

    onTickAsync: function (server) {
    }
};