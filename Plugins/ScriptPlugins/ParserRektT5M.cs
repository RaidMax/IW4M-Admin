#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// RektT5m parser. Ported from the legacy ParserRektT5M.js Jint script.
/// </summary>
public class ParserRektT5M : IPluginV2
{
    public string Name => "RektT5m Parser";
    public string Author => "RaidMax";
    public string Version => "0.4";

    public ParserRektT5M()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        const string version =
            "Call of Duty Multiplayer - Ship COD_T5_S MP build 7.0.189 CL(1022875) CODPCAB-V64 CEG Wed Nov 02 18:02:23 2011 win-x86";

        eventParser.Configuration.GameDirectory = "data";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿ" + (char)0x01 + "print\n";
        rcon.CommandPrefixes.Tell = "tell {0} {1}";
        rcon.CommandPrefixes.RConGetInfo = null;
        rcon.DefaultRConPort = 28960;

        rconParser.Version = version;
        rconParser.GameName = Server.Game.T5;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.T5;

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
