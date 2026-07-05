#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// IW4x parser. Ported from the legacy ParserIW4x.js Jint script.
/// </summary>
public class ParserIW4x : IParserDefinition
{
    public string Name => "IW4x Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Tell = "tellraw {0} {1}";
        rcon.CommandPrefixes.Say = "sayraw {0}";
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "tempbanclient {0} \"{1}\"";
        rcon.CommandPrefixes.Mute = "muteClient {0}";
        rcon.CommandPrefixes.Unmute = "unmute {0}";

        rcon.DefaultRConPort = 28960;
        rcon.DumpuserCommandFormat = "dumpuser {1}"; // per-client userinfo query — unverified on IW4x, graceful no-op if absent
        rcon.DefaultInstallationDirectoryHint =
            @"HKEY_CURRENT_USER\Software\Classes\iw4x\shell\open\command";
        rcon.FloodProtectInterval = 150;

        eventParser.Configuration.GameDirectory = "userraw";

        rconParser.Version = "IW4x (v0.6.0)";
        rconParser.GameName = Server.Game.IW4;
        eventParser.Version = "IW4x (v0.6.0)";
        eventParser.GameName = Server.Game.IW4;
        eventParser.URLProtocolFormat = "iw4x://{{ip}}:{{port}}";

    }
}
