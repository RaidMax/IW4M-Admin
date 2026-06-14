#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// BOIII (Black Ops 3) parser. Ported from the legacy ParserBOIII.js Jint script.
/// </summary>
public class ParserBOIII : IParserDefinition
{
    public string Name => "BOIII Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        const string version = "[local] ship win64 CODBUILD8-764 (3421987) Mon Dec 16 10:44:20 2019 10d27bef";

        var rcon = rconParser.Configuration;
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\d{1,3}\.\d{1,3}\.\d{1,3}.\d{1,3}|loopback|unknown|0+.0+)(?:\:-?\d{1,5})? +(-*[0-9]+) *$";
        rcon.StatusHeader.Pattern = "num +score +ping +xuid +name +address +qport *";
        rcon.CommandPrefixes.Kick = "clientkick {0}"; // clientkick_for_reason fails over rcon
        rcon.CommandPrefixes.Ban = "clientkick {0}";
        rcon.CommandPrefixes.TempBan = "clientkick_for_reason {0} \"{1}\"";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿ(" + (char)0x01 + "|print) ?";
        rcon.GametypeStatus.Pattern = "Gametype: (.+)";
        rcon.MapStatus.Pattern = "Map: (.+)";
        rcon.CommandPrefixes.RConGetInfo = null; // disables this, because it's useless on T7/BOIII
        rcon.ServerNotRunningResponse =
            "this is here to prevent a hibernating server from being detected as not running";
        rcon.DefaultRConPort = 27017;

        rcon.OverrideDvarNameMapping.Add("sv_hostname", "live_steam_server_name");
        rcon.OverrideDvarNameMapping.Add("g_password", "live_steam_server_password");
        rcon.DefaultDvarValues.Add("sv_running", "1");
        rcon.DefaultDvarValues.Add("g_gametype", "");
        rcon.DefaultDvarValues.Add("fs_basepath", "");
        rcon.DefaultDvarValues.Add("fs_basegame", "");
        rcon.DefaultDvarValues.Add("fs_homepath", "");
        rcon.DefaultDvarValues.Add("fs_game", "");

        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 6);
        rcon.GametypeStatus.AddMapping(ParserRegex.GroupType.RConStatusGametype, 1);

        rconParser.Version = version;
        rconParser.GameName = Server.Game.T7;
        rconParser.CanGenerateLogPath = false;

        eventParser.Version = version;
        eventParser.GameName = Server.Game.T7;
        eventParser.Configuration.GameDirectory = "usermaps";
        eventParser.Configuration.Say.Pattern = @"^(chat|chatteam);(?:[0-9]+);([a-f0-9]+);([0-9]+);(.+);(.*)$";

    }
}
