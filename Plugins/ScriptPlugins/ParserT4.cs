#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// Call of Duty 5: World at War parser. Ported from the legacy ParserT4.js Jint script.
/// </summary>
public class ParserT4 : IParserDefinition
{
    public string Name => "Call of Duty 5: World at War Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        const string version =
            "Call of Duty Multiplayer COD_WaW MP build 1.7.1263 CL(350073) JADAMS2 Thu Oct 29 15:43:55 2009 win-x86";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.DefaultRConPort = 28960;
        rconParser.Version = version;
        rconParser.GameName = Server.Game.T4;

        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        eventParser.GameName = Server.Game.T4;
        eventParser.Version = version;

    }
}
