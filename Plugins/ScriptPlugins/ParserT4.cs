#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Call of Duty 5: World at War parser. Ported from the legacy ParserT4.js Jint script.
/// </summary>
public class ParserT4 : IPluginV2
{
    public string Name => "Call of Duty 5: World at War Parser";
    public string Author => "RaidMax";
    public string Version => "0.3";

    public ParserT4()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

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

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
