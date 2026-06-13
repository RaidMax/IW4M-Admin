#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// Plutonium T5 (Black Ops) parser. Ported from the legacy ParserPlutoniumT5.js Jint script.
/// </summary>
public class ParserPlutoniumT5 : IParserDefinition
{
    public string Name => "Plutonium T5 Parser (2025)";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        const string version =
            "Call of Duty Multiplayer - Ship COD_T5_S MP build 7.0.189 CL(1022875) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86";

        eventParser.Configuration.GameDirectory = "main";

        var rcon = rconParser.Configuration;
        rcon.DefaultInstallationDirectoryHint = "{LocalAppData}/Plutonium/storage/t5";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.Dvar.Pattern =
            @"^(?:\^7)?\""(.+)\"" is: \""(.+)?\"" default: \""(.+)?\""\n?(?:latched: \""(.+)?\""\n)?\w*(.+)*$";
        rcon.CommandPrefixes.Tell = "tell {0} {1}";
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.DefaultRConPort = 28960;

        rconParser.Version = version;
        rconParser.GameName = Server.Game.T5;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.T5;
        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;

    }
}
