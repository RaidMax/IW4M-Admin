#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Black Ops 3 parser (T7 engine). Ported from the legacy ParserT7.js Jint script.
/// </summary>
public class ParserT7 : IPluginV2
{
    public string Name => "Black Ops 3 Parser";
    public string Author => "RaidMax, Future";
    public string Version => "0.6";

    public ParserT7()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        const string version = "[local] ship win64 CODBUILD8-764 (3421987) Mon Dec 16 10:44:20 2019 10d27bef (Retail)";

        var rcon = rconParser.Configuration;
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\d{1,3}\.\d{1,3}\.\d{1,3}.\d{1,3}|loopback|unknown|0+.0+)(?:\:-?\d{1,5})? +(-*[0-9]+) *$";
        rcon.StatusHeader.Pattern = "num +score +ping +xuid +name +address +qport|---------- Live ----------";
        rcon.CommandPrefixes.Kick = "clientkick {0}"; // clientkick_for_reason fails over rcon
        rcon.CommandPrefixes.Ban = "clientkick {0}";
        rcon.CommandPrefixes.TempBan = "tempbanclient {0}";
        rcon.CommandPrefixes.RConCommand = "ÿÿÿÿ" + (char)0x00 + "{0} {1}";
        rcon.CommandPrefixes.RConGetDvar = "ÿÿÿÿ" + (char)0x00 + "{0} {1}";
        rcon.CommandPrefixes.RConSetDvar = "ÿÿÿÿ" + (char)0x00 + "{0} set {1}";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿ" + (char)0x01;
        rcon.GametypeStatus.Pattern = "Gametype: (.+)";
        rcon.MapStatus.Pattern = "Map: (.+)";
        rcon.CommandPrefixes.RConGetInfo = null; // disables this, because it's useless on T7
        rcon.ServerNotRunningResponse =
            "this is here to prevent a hibernating server from being detected as not running";
        rcon.DefaultRConPort = 27016;

        rcon.OverrideDvarNameMapping.Add("sv_hostname", "live_steam_server_name");
        rcon.OverrideDvarNameMapping.Add("g_password", "live_steam_server_password");
        rcon.DefaultDvarValues.Add("sv_running", "1");
        rcon.DefaultDvarValues.Add("g_gametype", "");
        rcon.DefaultDvarValues.Add("fs_basepath", "");
        rcon.DefaultDvarValues.Add("fs_basegame", "");
        rcon.DefaultDvarValues.Add("fs_homepath", "");
        rcon.DefaultDvarValues.Add("fs_game", "");

        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 6);
        rcon.GametypeStatus.AddMapping(ParserRegex.GroupType.RConStatusGametype, 1);

        rconParser.Version = version;
        rconParser.GameName = Server.Game.T7;
        rconParser.CanGenerateLogPath = false;

        eventParser.Version = version;
        eventParser.GameName = Server.Game.T7;
        eventParser.Configuration.GameDirectory = "usermaps";
        eventParser.Configuration.Say.Pattern = @"^(chat|chatteam);(?:[0-9]+);([0-9]+);([0-9]+);(.+);(.*)$";

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
