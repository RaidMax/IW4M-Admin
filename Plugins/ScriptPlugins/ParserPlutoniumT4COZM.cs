#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// Plutonium T4 (Call of Duty: World at War) co-op / zombies parser.
/// Registers dynamic RCon + event parsers so IW4MAdmin can monitor Plutonium-hosted T4 ZM/co-op servers.
/// Ported from the legacy ParserPlutoniumT4COZM.js Jint script.
/// </summary>
public class ParserPlutoniumT4Cozm : IParserDefinition
{
    public string Name => "Plutonium T4 CO-OP/Zombies Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        var rcon = rconParser.Configuration;
        // co-op/zombies clientkick takes no reason argument
        rcon.CommandPrefixes.Kick = "clientkick {0}";
        rcon.CommandPrefixes.Ban = "clientkick {0}";
        rcon.CommandPrefixes.TempBan = "clientkick {0}";
        // JS used '\xff\xff\xff\xffprint\n'; ÿ is the unambiguous fixed-width equivalent
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.CommandPrefixes.RConGetInfo = null; // disabled on T4 co-op/zombies
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.DefaultRConPort = 28960;
        rcon.OverrideDvarNameMapping.Add("fs_homepath", "fs_localAppData");
        rcon.DefaultInstallationDirectoryHint = "{LocalAppData}/Plutonium/storage/t4";

        rconParser.Version = "Plutonium T4 Singleplayer";
        rconParser.GameName = Server.Game.T4;

        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        eventParser.Configuration.GameDirectory = "main";
        eventParser.Version = "Plutonium T4 Singleplayer";

    }
}
