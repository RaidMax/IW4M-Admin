#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// H1-Mod parser. Ported from the legacy ParserH1MOD.js Jint script.
/// </summary>
public class ParserH1MOD : IPluginV2
{
    public string Name => "H1-Mod Parser";
    public string Author => "alice, diamante0018";
    public string Version => "0.2";

    public ParserH1MOD()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        const string version = "H1 MP 1.15 build 1251288 Tue Jul 23 13:38:30 2019 win64";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Kick = "kickClient {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "kickClient {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "kickClient {0} \"{1}\"";
        rcon.CommandPrefixes.Tell = "tellraw {0} \"{1}\"";
        rcon.CommandPrefixes.Say = "sayraw \"{0}\"";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint";
        rcon.Dvar.Pattern = @"^ *\""(.+)\"" is: \""(.+)?\"" default: \""(.+)?\""";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +(Yes|No) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$";
        rcon.StatusHeader.Pattern = "num +score +bot +ping +guid +name +address +qport *";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 6);
        rcon.WaitForResponse = false;
        rcon.DefaultRConPort = 27016;

        eventParser.Configuration.GameDirectory = "";
        eventParser.Configuration.LocalizeText = ((char)0x1f).ToString();

        rconParser.Version = version;
        rconParser.GameName = Server.Game.H1;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.H1;

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
