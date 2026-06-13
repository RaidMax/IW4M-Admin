#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

/// <summary>
/// CS:GO parser (Source engine). Ported from the legacy ParserCSGO.js Jint script.
/// </summary>
public class ParserCSGO : IParserDefinition
{
    public string Name => "CS:GO Parser";

    private const string Engine = "Source";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {
        rconParser.RConEngine = Engine;

        var rcon = rconParser.Configuration;
        rcon.StatusHeader.Pattern = "userid +name +uniqueid +connected +ping +loss +state +rate +adr";

        rcon.MapStatus.Pattern = "^map *: +(.+)$";
        rcon.MapStatus.AddMapping(ParserRegex.GroupType.RConStatusMap, 1);

        rcon.HostnameStatus.Pattern = "^hostname: +(.+)$";
        rcon.HostnameStatus.AddMapping(ParserRegex.GroupType.RConStatusHostname, 1);

        rcon.MaxPlayersStatus.Pattern = @"^players *: +\d+ humans, \d+ bots \((\d+).+";
        rcon.MaxPlayersStatus.AddMapping(ParserRegex.GroupType.RConStatusMaxPlayers, 1);

        rcon.Dvar.Pattern = @"^""(.+)"" = ""(.+)"" (?:\( def. ""(.*)"" \))?(?: |\w)+- (.+)$";
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarName, 1);
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarValue, 2);
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarDefaultValue, 3);
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarLatchedValue, 3);

        rcon.Status.Pattern =
            @"^#\s*(\d+) (\d+) ""(.+)"" (\S+) +(\d+:\d+(?::\d+)?) (\d+) (\S+) (\S+) (\d+) (\d+\.\d+\.\d+\.\d+:\d+)$";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConClientNumber, 2);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConScore, -1);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 6);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 3);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 10);
        rcon.Status.AddMapping(ParserRegex.GroupType.AdditionalGroup, 1);

        rcon.DefaultDvarValues.Add("sv_running", "1");
        rcon.DefaultDvarValues.Add("version", Engine);
        rcon.DefaultDvarValues.Add("fs_basepath", "");
        rcon.DefaultDvarValues.Add("fs_basegame", "");
        rcon.DefaultDvarValues.Add("fs_homepath", "");
        rcon.DefaultDvarValues.Add("g_log", "");
        rcon.DefaultDvarValues.Add("net_ip", "localhost");

        rcon.OverrideDvarNameMapping.Add("sv_hostname", "hostname");
        rcon.OverrideDvarNameMapping.Add("mapname", "host_map");
        rcon.OverrideDvarNameMapping.Add("sv_maxclients", "maxplayers");
        rcon.OverrideDvarNameMapping.Add("g_gametype", "game_type");
        rcon.OverrideDvarNameMapping.Add("fs_game", "game_mode");
        rcon.OverrideDvarNameMapping.Add("g_password", "sv_password");

        rcon.ColorCodeMapping.Clear();
        rcon.ColorCodeMapping.Add("White", ((char)0x01).ToString());
        rcon.ColorCodeMapping.Add("Red", ((char)0x07).ToString());
        rcon.ColorCodeMapping.Add("LightRed", ((char)0x0F).ToString());
        rcon.ColorCodeMapping.Add("DarkRed", ((char)0x02).ToString());
        rcon.ColorCodeMapping.Add("Blue", ((char)0x0B).ToString());
        rcon.ColorCodeMapping.Add("DarkBlue", ((char)0x0C).ToString());
        rcon.ColorCodeMapping.Add("Purple", ((char)0x03).ToString());
        rcon.ColorCodeMapping.Add("Orchid", ((char)0x0E).ToString());
        rcon.ColorCodeMapping.Add("Yellow", ((char)0x09).ToString());
        rcon.ColorCodeMapping.Add("Gold", ((char)0x10).ToString());
        rcon.ColorCodeMapping.Add("LightGreen", ((char)0x05).ToString());
        rcon.ColorCodeMapping.Add("Green", ((char)0x04).ToString());
        rcon.ColorCodeMapping.Add("Lime", ((char)0x06).ToString());
        rcon.ColorCodeMapping.Add("Grey", ((char)0x08).ToString());
        rcon.ColorCodeMapping.Add("Grey2", ((char)0x0D).ToString());
        // only adding this here for the default accent color
        rcon.ColorCodeMapping.Add("Cyan", ((char)0x0B).ToString());

        rcon.NoticeLineSeparator = ". ";
        rcon.DefaultRConPort = 27015;
        rconParser.CanGenerateLogPath = false;

        rcon.CommandPrefixes.RConGetInfo = null;
        rcon.CommandPrefixes.Kick = "kickid {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "kickid {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "kickid {0} \"{1}\"";
        rcon.CommandPrefixes.Say = "say {0}";
        rcon.CommandPrefixes.Tell = "say [{0}] {1}"; // no tell exists in vanilla

        var evt = eventParser.Configuration;
        evt.Say.Pattern = @"^""(.+)<(\d+)><(.+)><(.*?)>"" (?:say|say_team) ""(.*)""$";
        evt.Say.AddMapping(ParserRegex.GroupType.OriginName, 1);
        evt.Say.AddMapping(ParserRegex.GroupType.OriginClientNumber, 2);
        evt.Say.AddMapping(ParserRegex.GroupType.OriginNetworkId, 3);
        evt.Say.AddMapping(ParserRegex.GroupType.OriginTeam, 4);
        evt.Say.AddMapping(ParserRegex.GroupType.Message, 5);

        evt.Kill.Pattern =
            @"^""(.+)<(\d+)><(.+)><(.*)>"" \[-?\d+ -?\d+ -?\d+\] killed ""(.+)<(\d+)><(.+)><(.*)>"" \[-?\d+ -?\d+ -?\d+\] with ""(\S*)"" *(?:\((\w+)((?: ).+)?\))?$";
        evt.Kill.AddMapping(ParserRegex.GroupType.OriginName, 1);
        evt.Kill.AddMapping(ParserRegex.GroupType.OriginClientNumber, 2);
        evt.Kill.AddMapping(ParserRegex.GroupType.OriginNetworkId, 3);
        evt.Kill.AddMapping(ParserRegex.GroupType.OriginTeam, 4);
        evt.Kill.AddMapping(ParserRegex.GroupType.TargetName, 5);
        evt.Kill.AddMapping(ParserRegex.GroupType.TargetClientNumber, 6);
        evt.Kill.AddMapping(ParserRegex.GroupType.TargetNetworkId, 7);
        evt.Kill.AddMapping(ParserRegex.GroupType.TargetTeam, 8);
        evt.Kill.AddMapping(ParserRegex.GroupType.Weapon, 9);
        evt.Kill.AddMapping(ParserRegex.GroupType.HitLocation, 10);

        evt.MapEnd.Pattern = @"^World triggered ""Match_Start"" on ""(.+)""$";

        evt.JoinTeam.Pattern = @"^""(.+)<(\d+)><(.*)>"" switched from team <(.+)> to <(.+)>$";
        evt.JoinTeam.AddMapping(ParserRegex.GroupType.OriginName, 1);
        evt.JoinTeam.AddMapping(ParserRegex.GroupType.OriginClientNumber, 2);
        evt.JoinTeam.AddMapping(ParserRegex.GroupType.OriginNetworkId, 3);
        evt.JoinTeam.AddMapping(ParserRegex.GroupType.OriginTeam, 5);

        evt.TeamMapping.Add("CT", EFClient.TeamType.Allies);
        evt.TeamMapping.Add("TERRORIST", EFClient.TeamType.Axis);

        evt.Time.Pattern = @"^L [01]\d/[0-3]\d/\d+ - [0-2]\d:[0-5]\d:[0-5]\d:";

        rconParser.Version = "CSGO";
        rconParser.GameName = Server.Game.CSGO;
        eventParser.Version = "CSGO";
        eventParser.GameName = Server.Game.CSGO;
        eventParser.URLProtocolFormat = "steam://connect/{{ip}}:{{port}}";

    }
}
