#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// IW4x parser. Ported from the legacy ParserIW4x.js Jint script.
/// </summary>
public class ParserIW4x : IPluginV2
{
    public string Name => "IW4x Parser";
    public string Author => "RaidMax";
    public string Version => "0.6";

    public ParserIW4x()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Tell = "tellraw {0} {1}";
        rcon.CommandPrefixes.Say = "sayraw {0}";
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "tempbanclient {0} \"{1}\"";
        rcon.CommandPrefixes.Mute = "muteClient {0}";
        rcon.CommandPrefixes.Unmute = "unmute {0}";

        rcon.DefaultRConPort = 28960;
        rcon.DefaultInstallationDirectoryHint =
            @"HKEY_CURRENT_USER\Software\Classes\iw4x\shell\open\command";
        rcon.FloodProtectInterval = 150;

        eventParser.Configuration.GameDirectory = "userraw";

        rconParser.Version = "IW4x (v0.6.0)";
        rconParser.GameName = Server.Game.IW4;
        eventParser.Version = "IW4x (v0.6.0)";
        eventParser.GameName = Server.Game.IW4;
        eventParser.URLProtocolFormat = "iw4x://{{ip}}:{{port}}";

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
