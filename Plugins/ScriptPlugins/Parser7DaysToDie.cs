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
        rcon.StatusHeader.Pattern = "slot score kills deaths ping networkid name address";

        rcon.HostnameStatus.Pattern = "^hostname: (.+)$";
        rcon.HostnameStatus.AddMapping(ParserRegex.GroupType.RConStatusHostname, 1);
        rcon.MapStatus.Pattern = "^map: (.+)$";
        rcon.MapStatus.AddMapping(ParserRegex.GroupType.RConStatusMap, 1);
        rcon.GametypeStatus.Pattern = "^gametype: (.+)$";
        rcon.GametypeStatus.AddMapping(ParserRegex.GroupType.RConStatusGametype, 1);
        rcon.MaxPlayersStatus.Pattern = @"^players: \d+/(\d+)$";
        rcon.MaxPlayersStatus.AddMapping(ParserRegex.GroupType.RConStatusMaxPlayers, 1);

        rcon.Status.Pattern =
            @"^(\d+) +(-?\d+) +(\d+) +(\d+) +(\d+) +(\d+) +""([^""\r\n]*)"" +(\d{1,3}(?:\.\d{1,3}){3}):\d+$";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConClientNumber, 1);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConScore, 2);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConKills, 3);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConDeaths, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 6);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 7);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 8);

        rcon.DefaultDvarValues.Add("version", version);
        rcon.DefaultDvarValues.Add("sv_running", "1");
        rcon.DefaultDvarValues.Add("sv_hostname", "7 Days to Die Server");
        rcon.DefaultDvarValues.Add("sv_maxclients", "8");
        rcon.DefaultDvarValues.Add("g_gametype", "Survival");
        rcon.DefaultDvarValues.Add("fs_basepath", "");
        rcon.DefaultDvarValues.Add("fs_basegame", "");
        rcon.DefaultDvarValues.Add("fs_homepath", "");
        rcon.DefaultDvarValues.Add("fs_game", "");
        rcon.DefaultDvarValues.Add("g_log", "");
        rcon.DefaultDvarValues.Add("g_logsync", "2");
        rcon.DefaultDvarValues.Add("net_ip", "0.0.0.0");
        rcon.DefaultDvarValues.Add("g_password", "");
        rcon.DefaultDvarValues.Add("sv_privateClients", "0");

        rcon.CommandPrefixes.RConGetInfo = null;
        rcon.CommandPrefixes.Kick = "kick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = rcon.CommandPrefixes.Kick;
        rcon.CommandPrefixes.TempBan = rcon.CommandPrefixes.Kick;
        rcon.CommandPrefixes.Say = "say \"{0}\"";
        rcon.CommandPrefixes.Tell = "tell {0} \"{1}\"";
        rcon.NoticeLineSeparator = " ";

        rcon.ColorCodeMapping.Clear();
        foreach (var color in new[]
                 {
                     "Black", "Red", "Green", "Yellow", "Blue", "Cyan", "Pink", "White", "Map", "Grey",
                     "Wildcard"
                 })
        {
            rcon.ColorCodeMapping.Add(color, "");
        }

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

        eventParser.Version = version;
        eventParser.GameName = Server.Game.D7D;
        eventParser.URLProtocolFormat = "steam://connect/{{ip}}:26900";
    }
}
