#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// Plutonium T4 (Call of Duty: World at War) multiplayer parser.
/// Registers dynamic RCon + event parsers so IW4MAdmin can monitor Plutonium-hosted T4 servers.
/// Ported from the legacy ParserPlutoniumT4.js Jint script.
/// </summary>
public class ParserPlutoniumT4 : IParserDefinition
{
    public string Name => "Plutonium T4 MP Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "clientkick {0} \"{1}\"";
        // JS used '\xff\xff\xff\xffprint\n'; ÿ is the unambiguous fixed-width equivalent
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.DefaultRConPort = 28960;
        rcon.DumpuserCommandFormat = "dumpuser {1}"; // per-client userinfo query — T4 resolves by NAME, not slot (verified)
        rcon.OverrideDvarNameMapping.Add("fs_homepath", "fs_localAppData");
        rcon.DefaultInstallationDirectoryHint = "{LocalAppData}/Plutonium/storage/t4";

        rconParser.Version = "Plutonium T4";
        rconParser.GameName = Server.Game.T4;

        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        eventParser.Configuration.GameDirectory = "main";
        eventParser.Version = "Plutonium T4";

    }
}
