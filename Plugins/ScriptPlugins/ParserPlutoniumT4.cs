#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Plutonium T4 (Call of Duty: World at War) multiplayer parser.
/// Registers dynamic RCon + event parsers so IW4MAdmin can monitor Plutonium-hosted T4 servers.
/// Ported from the legacy ParserPlutoniumT4.js Jint script.
/// </summary>
public class ParserPlutoniumT4 : IPluginV2
{
    public string Name => "Plutonium T4 MP Parser";
    public string Author => "RaidMax, Chase, Future";
    public string Version => "0.5";

    public ParserPlutoniumT4()
    {
        // The Load event fires during ApplicationManager.Init, before servers resolve their
        // parsers in InitializeServers() - so parsers added here are present in time.
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "clientkick {0} \"{1}\"";
        // JS used '\xff\xff\xff\xffprint\n'; ÿ is the unambiguous fixed-width equivalent
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.GuidNumberStyle = NumberStyles.Integer;
        rcon.DefaultRConPort = 28960;
        rcon.OverrideDvarNameMapping.Add("fs_homepath", "fs_localAppData");
        rcon.DefaultInstallationDirectoryHint = "{LocalAppData}/Plutonium/storage/t4";

        rconParser.Version = "Plutonium T4";
        rconParser.GameName = Server.Game.T4;

        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        eventParser.Configuration.GameDirectory = "main";
        eventParser.Version = "Plutonium T4";

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
