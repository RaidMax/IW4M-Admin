#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// IW6x parser. Ported from the legacy ParserIW6x.js Jint script.
/// </summary>
public class ParserIW6x : IParserDefinition
{
    public string Name => "IW6x Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Tell = "tell {0} {1}";
        rcon.CommandPrefixes.Say = "say {0}";
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";
        rcon.CommandPrefixes.Mute = "muteClient {0}";
        rcon.CommandPrefixes.Unmute = "unmuteClient {0}";
        rcon.Dvar.Pattern =
            @"^ *\""(.+)\"" is: \""(.+)?\"" default: \""(.+)?\""\n?(?:latched: \""(.+)?\""\n?)?(.*)$";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +(Yes|No) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$";
        rcon.StatusHeader.Pattern = "num +score +bot +ping +guid +name +address +qport *";
        rcon.WaitForResponse = false;
        rcon.DefaultRConPort = 28960;

        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 6);

        rconParser.Version = "IW6 MP 3.15 build 2 Sat Sep 14 2013 03:58:30PM win64";
        rconParser.GameName = Server.Game.IW6;
        eventParser.Version = "IW6 MP 3.15 build 2 Sat Sep 14 2013 03:58:30PM win64";
        eventParser.GameName = Server.Game.IW6;

        eventParser.Configuration.GameDirectory = "";
        eventParser.Configuration.LocalizeText = ((char)0x1f).ToString();

    }
}
