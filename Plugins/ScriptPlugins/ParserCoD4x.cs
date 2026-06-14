#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// CoD4x parser. Ported from the legacy ParserCoD4x.js Jint script.
/// </summary>
public class ParserCoD4x : IParserDefinition
{
    public string Name => "CoD4x Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        var rcon = rconParser.Configuration;
        rcon.StatusHeader.Pattern =
            "num +score +ping +playerid +steamid +name +lastmsg +address +qport +rate *";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]{16,32})|0) +([[0-9]+|0]) +(.{0,34}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|bot) +(-*[0-9]+) +([0-9]+) *$";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 6);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 8);
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";

        rcon.Dvar.Pattern = @"^""(.+)"" is: ""(.+)?"" default: ""(.+)?"" info: ""(.+)?""$";
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarLatchedValue, 2);
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarDomain, 4);
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.NoticeLineSeparator = ". "; // CoD4x does not support \n in the client notice
        rcon.DefaultRConPort = 28960;

        rconParser.Version = "CoD4 X - win_mingw-x86 build 1056 Dec 12 2020";
        rconParser.GameName = Server.Game.IW3;

        eventParser.Configuration.GameDirectory = "main";
        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        eventParser.Version = "CoD4 X - win_mingw-x86 build 1056 Dec 12 2020";
        eventParser.GameName = Server.Game.IW3;
        eventParser.URLProtocolFormat = "cod4://{{ip}}:{{port}}";

    }
}
