#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Tekno MW3 parser. Ported from the legacy ParserTeknoMW3.js Jint script.
/// </summary>
public class ParserTeknoMW3 : IPluginV2
{
    public string Name => "Tekno MW3 Parser";
    public string Author => "RaidMax";
    public string Version => "0.9";

    public ParserTeknoMW3()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        const string version = "IW5 MP 1.4 build 382 latest Thu Jan 19 2012 11:09:49AM win-x86";

        var rcon = rconParser.Configuration;
        rcon.Status.Pattern =
            @"^ *([0-9]+) +([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[A-Z]|[0-9]){16,32}|0)\t +(.{0,16}) +([0-9]+) +(\d+\.\d+\.\d+\.\d+\:-?\d{1,5}|0+\.0+\:-?\d{1,5}|loopback) *$";
        rcon.StatusHeader.Pattern = "num +score +ping +guid +name +lastmsg +address";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 4);
        rcon.CommandPrefixes.RConGetInfo = null;
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿ(print)?";
        rcon.CommandPrefixes.Tell = "tell {0} {1}";
        rcon.CommandPrefixes.Say = "say {0}";
        rcon.CommandPrefixes.Kick = "dropclient {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "dropclient {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "dropclient {0} \"{1}\"";
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarValue, 1);
        rcon.Dvar.Pattern = "^(.*)$";
        rcon.NoticeLineSeparator = ". ";
        rcon.DefaultRConPort = 8766;

        rcon.DefaultDvarValues.Add("sv_running", "1");
        rcon.OverrideDvarNameMapping.Add("_website", "sv_clanWebsite");

        rconParser.Version = version;
        rconParser.GameName = Server.Game.IW5;
        rconParser.CanGenerateLogPath = false;

        eventParser.Configuration.GameDirectory = "scripts";
        eventParser.Version = version;
        eventParser.GameName = Server.Game.IW5;

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
