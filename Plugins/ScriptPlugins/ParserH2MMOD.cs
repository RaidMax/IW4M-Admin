#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// H2M-Mod parser. Ported from the legacy ParserH2MMOD.js Jint script.
/// </summary>
public class ParserH2MMOD : IParserDefinition
{
    public string Name => "H2M-Mod Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        const string version = "H1 MP 1.15 build 1251288 Tue Jul 23 13:38:30 2019 win64";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Kick = "kickClient {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "kickClient {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "kickClient {0} \"{1}\"";
        rcon.CommandPrefixes.Tell = "tellraw {0} \"{1}\"";
        rcon.CommandPrefixes.Say = "sayraw \"{0}\"";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint";
        rcon.Dvar.Pattern = @"^ *\""(.+)\"" is: \""(.+)?\"" default: \""(.+)?\""";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +(Yes|No) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$";
        rcon.StatusHeader.Pattern = "num +score +bot +ping +guid +name +address +qport *";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 6);
        rcon.WaitForResponse = false;
        rcon.DefaultRConPort = 27016;

        eventParser.Configuration.GameDirectory = "";
        eventParser.Configuration.LocalizeText = ((char)0x1f).ToString();

        rconParser.Version = version;
        rconParser.GameName = Server.Game.H2M;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.H2M;

    }
}
