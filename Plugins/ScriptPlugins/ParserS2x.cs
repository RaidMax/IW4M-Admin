#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// S2x (Call of Duty: WWII) parser.
/// </summary>
public class ParserS2x : IParserDefinition
{
    public string Name => "S2x Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        const string version = "S2 MP 2.20 build 0 Sun Jan 11 05:14:23 2026 win64";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Tell = "tellraw {0} \"{1}\"";
        rcon.CommandPrefixes.Say = "sayraw \"{0}\"";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.CommandPrefixes.RConGetInfo = "ÿÿÿÿs2x_getInfo";
        rcon.CommandPrefixes.RconGetInfoResponseHeader = "ÿÿÿÿs2x_infoResponse";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,35}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) +([0-9]+) *$";
        rcon.GametypeStatus.Pattern = "^gametype: (.+)$";
        rcon.GametypeStatus.AddMapping(ParserRegex.GroupType.RConStatusGametype, 1);
        rcon.DefaultRConPort = 27016;

        eventParser.Configuration.GameDirectory = "";

        rconParser.IsOneLog = true;
        rconParser.Version = version;
        rconParser.GameName = Server.Game.S2;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.S2;

    }
}
