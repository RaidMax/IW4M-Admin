#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// Parser for the native 7 Days to Die Telnet console and server output log.
/// </summary>
public sealed class Parser7DaysToDie : IParserDefinition
{
    public string Name => "7 Days to Die Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {
        const string version = "7DTD";

        rconParser.RConEngine = version;
        rconParser.Version = version;
        rconParser.GameName = Server.Game.D7D;
        rconParser.CanGenerateLogPath = false;

        var rcon = rconParser.Configuration;
        rcon.DefaultRConPort = 8081;
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.FloodProtectInterval = 100;
        rcon.StatusHeader.Pattern = "slot score kills deaths ping networkid name address entityid";

        rcon.HostnameStatus.Pattern = "^hostname: (.+)$";
        rcon.HostnameStatus.AddMapping(ParserRegex.GroupType.RConStatusHostname, 1);
        rcon.MapStatus.Pattern = "^map: (.+)$";
        rcon.MapStatus.AddMapping(ParserRegex.GroupType.RConStatusMap, 1);
        rcon.GametypeStatus.Pattern = "^gametype: (.+)$";
        rcon.GametypeStatus.AddMapping(ParserRegex.GroupType.RConStatusGametype, 1);
        rcon.MaxPlayersStatus.Pattern = @"^players: \d+/(\d+)$";
        rcon.MaxPlayersStatus.AddMapping(ParserRegex.GroupType.RConStatusMaxPlayers, 1);

        rcon.Status.Pattern =
            @"^(\d+) +(-?\d+) +(\d+) +(\d+) +(\d+) +(\d+) +""([^""\r\n]*)"" +(\d{1,3}(?:\.\d{1,3}){3}):\d+ +(\d+)$";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConClientNumber, 1);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConScore, 2);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConStatusKills, 3);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConStatusDeaths, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 6);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 7);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 8);
        rcon.Status.AddMapping(ParserRegex.GroupType.AdditionalGroup, 9);

        rcon.DefaultDvarValues["version"] = version;
        rcon.DefaultDvarValues["sv_running"] = "1";
        rcon.DefaultDvarValues["sv_hostname"] = "7 Days to Die Server";
        rcon.DefaultDvarValues["mapname"] = "Unknown";
        rcon.DefaultDvarValues["sv_maxclients"] = "8";
        rcon.DefaultDvarValues["g_gametype"] = "Survival";
        rcon.DefaultDvarValues["fs_basepath"] = "";
        rcon.DefaultDvarValues["fs_basegame"] = "";
        rcon.DefaultDvarValues["fs_homepath"] = "";
        rcon.DefaultDvarValues["fs_game"] = "";
        rcon.DefaultDvarValues["g_log"] = "";
        rcon.DefaultDvarValues["g_logsync"] = "2";
        rcon.DefaultDvarValues["net_ip"] = "0.0.0.0";
        rcon.DefaultDvarValues["g_password"] = "";
        rcon.DefaultDvarValues["sv_privateClients"] = "0";

        rcon.CommandPrefixes.RConGetInfo = null;
        rcon.CommandPrefixes.Kick = "kick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = rcon.CommandPrefixes.Kick;
        rcon.CommandPrefixes.TempBan = rcon.CommandPrefixes.Kick;
        rcon.CommandPrefixes.Say = "say \"{0}\"";
        rcon.CommandPrefixes.Tell = "tell {0} \"{1}\"";
        rcon.NoticeLineSeparator = " ";

        rcon.ColorCodeMapping.Clear();

        var events = eventParser.Configuration;
        events.GuidNumberStyle = NumberStyles.Integer;
        events.Time.Pattern =
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?\s+\d+(?:\.\d+)?\s+(?:INF|WRN|ERR|EXC)\s+";
        events.Say.Pattern =
            @"^.*Chat \(from '(?:Steam_)?(\d+)', entity id '(\d+)', to '[^']+'\): '([^']*)': (.*)$";
        events.Say.AddMapping(ParserRegex.GroupType.OriginNetworkId, 1);
        events.Say.AddMapping(ParserRegex.GroupType.OriginClientNumber, 2);
        events.Say.AddMapping(ParserRegex.GroupType.OriginName, 3);
        events.Say.AddMapping(ParserRegex.GroupType.Message, 4);

        // 7DTD records PvP and world deaths as name-only GMSG entries. The game log
        // event pipeline resolves these names against the connected client snapshot
        // before dispatching the standard IW4MAdmin kill event.
        events.Kill.Pattern =
            @"^GMSG: Player '(.+)' (?:(?:killed by '(.+)')|died)\s*$";
        events.Kill.AddMapping(ParserRegex.GroupType.TargetName, 1);
        events.Kill.AddMapping(ParserRegex.GroupType.OriginName, 2);

        eventParser.Version = version;
        eventParser.GameName = Server.Game.D7D;
        eventParser.URLProtocolFormat = "steam://connect/{{ip}}:{{port}}";
    }
}
