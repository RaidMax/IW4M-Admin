#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Plutonium T6 (Black Ops 2) parser. Ported from the legacy ParserPlutoniumT6.js Jint script.
/// </summary>
public class ParserPlutoniumT6 : IPluginV2
{
    public string Name => "Plutonium T6 Parser (2024)";
    public string Author => "RaidMax, Xerxes, INSANEMODE";
    public string Version => "1.0";

    public ParserPlutoniumT6()
    {
        IManagementEventSubscriptions.Load += OnLoad;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        var rconParser = manager.GenerateDynamicRConParser(Name);
        var eventParser = manager.GenerateDynamicEventParser(Name);

        const string version =
            "Call of Duty Multiplayer - Ship COD_T6_S MP build 1.0.44 CL(1759941) CODPCAB2 CEG Fri May 9 19:19:19 2014 win-x86 813e66d5";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Tell = "tell {0} {1}";
        rcon.CommandPrefixes.Say = "say {0}";
        rcon.CommandPrefixes.Kick = "clientkick_for_reason {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick_for_reason {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "clientkick_for_reason {0} \"{1}\"";
        rcon.CommandPrefixes.Mute = "muteClient {0}";
        rcon.CommandPrefixes.Unmute = "unmuteClient {0}";
        rcon.CommandPrefixes.RConGetDvar = "ÿÿÿÿrcon {0} {1}";
        rcon.CommandPrefixes.RConSetDvar = "ÿÿÿÿrcon {0} set {1}";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.CommandPrefixes.RConGetStatus = "ÿÿÿÿgetstatus";
        rcon.CommandPrefixes.RConGetInfo = "ÿÿÿÿgetinfo";
        rcon.CommandPrefixes.RconGetInfoResponseHeader = "ÿÿÿÿinfoResponse\n";
        rcon.CommandPrefixes.RConCommand = "ÿÿÿÿrcon {0} {1}";

        rcon.Dvar.Pattern =
            @"^(?:\^7)?\""(.+)\"" is: \""(.+)?\"" default: \""(.+)?\""\n?(?:latched: \""(.+)?\""\n)?\w*(.+)*$";
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarName, 1);
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarValue, 2);
        rcon.WaitForResponse = false;
        rcon.NoticeLineSeparator = ". ";
        rcon.DefaultRConPort = 4976;
        rcon.DefaultInstallationDirectoryHint = "{LocalAppData}/Plutonium/storage/t6";
        rcon.ShouldRemoveDiacritics = true;

        rcon.StatusHeader.Pattern = "num +score +bot +ping +guid +name +lastmsg +address +qport +rate *";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +([0-9]+) +(?:[0-1]{1}) +([0-9]+) +([A-F0-9]+|0) +(.+?) +(?:[0-9]+) +(\d+\.\d+\.\d+\.\d+\:-?\d{1,5}|0+\.0+:-?\d{1,5}|loopback|unknown|bot) +(?:-?[0-9]+) +(?:[0-9]+) *$";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConClientNumber, 1);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConScore, 2);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 3);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 6);

        // this is mostly default but just an example on how to map
        rcon.ColorCodeMapping.Clear();
        rcon.ColorCodeMapping.Add("Black", "^0");
        rcon.ColorCodeMapping.Add("Red", "^1");
        rcon.ColorCodeMapping.Add("Green", "^2");
        rcon.ColorCodeMapping.Add("Yellow", "^3");
        rcon.ColorCodeMapping.Add("Blue", "^4");
        rcon.ColorCodeMapping.Add("Cyan", "^5");
        rcon.ColorCodeMapping.Add("Pink", "^6");
        rcon.ColorCodeMapping.Add("White", "^7");
        rcon.ColorCodeMapping.Add("Map", "^8");
        rcon.ColorCodeMapping.Add("Grey", "^9");
        rcon.ColorCodeMapping.Add("LightBlue", "^;");
        rcon.ColorCodeMapping.Add("LightYellow", "^:");
        rcon.ColorCodeMapping.Add("Flash", "^F");
        rcon.ColorCodeMapping.Add("MWDOWN", "^BmouseWheelDown^");
        rcon.ColorCodeMapping.Add("MWUP", "^BmouseWheelUp^");
        rcon.ColorCodeMapping.Add("MBMiddle", "^BmouseButtonMiddle^");
        rcon.ColorCodeMapping.Add("MBRight", "^BmouseButtonRight^");
        rcon.ColorCodeMapping.Add("MBLeft", "^BmouseButtonLeft^");
        rcon.ColorCodeMapping.Add("MADown", "^BMOUSE_ANIM_DOWN^");
        rcon.ColorCodeMapping.Add("MAUp", "^BMOUSE_ANIM_UP^");
        rcon.ColorCodeMapping.Add("MARight", "^BMOUSE_ANIM_RIGHT^");
        rcon.ColorCodeMapping.Add("MALeft", "^BMOUSE_ANIM_LEFT^");

        rcon.FloodProtectInterval = 150;

        eventParser.Configuration.GameDirectory = "main";
        eventParser.Configuration.GuidNumberStyle = NumberStyles.Integer;

        rconParser.Version = version;
        rconParser.GameName = Server.Game.T6;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.T6;

        manager.AddOrReplaceRConParser(rconParser);
        manager.AddOrReplaceEventParser(eventParser);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.Load -= OnLoad;
    }
}
