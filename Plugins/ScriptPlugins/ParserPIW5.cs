#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

/// <summary>
/// Plutonium IW5 parser. Ported from the legacy ParserPIW5.js Jint script.
/// </summary>
public class ParserPIW5 : IParserDefinition
{
    public string Name => "Plutonium IW5 Parser";

    public void Configure(IRConParser rconParser, IEventParser eventParser)
    {

        const string version = "IW5 MP 1.9 build 388110 Fri Sep 14 00:04:28 2012 win-x86";

        var rcon = rconParser.Configuration;
        rcon.CommandPrefixes.Tell = "tell {0} {1}";
        rcon.CommandPrefixes.Say = "say {0}";
        rcon.CommandPrefixes.Kick = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.Ban = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.TempBan = "clientkick {0} \"{1}\"";
        rcon.CommandPrefixes.RConGetDvar = "ÿÿÿÿrcon {0} get {1}";
        rcon.CommandPrefixes.RConResponse = "ÿÿÿÿprint\n";

        rcon.Dvar.Pattern = @"^(.+) is ""(.+)?""";
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarName, 1);
        rcon.Dvar.AddMapping(ParserRegex.GroupType.RConDvarValue, 2);
        rcon.WaitForResponse = true;
        // NOTE: the original JS set rconParser.Configuration.CanGenerateLogPath here, which was a
        // no-op (the property lives on the parser, not its configuration), so it is intentionally
        // not ported - the parser keeps its default CanGenerateLogPath.
        rcon.NoticeLineSeparator = ". ";
        rcon.DefaultRConPort = 27016;
        rcon.DefaultInstallationDirectoryHint = "{LocalAppData}/Plutonium/storage/iw5";

        rcon.StatusHeader.Pattern = "num +score +bot +ping +guid +name +address +qport *";
        rcon.Status.Pattern =
            @"^ *([0-9]+) +-?([0-9]+) +(0|1) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown|bot) +(-*[0-9]+) *$";
        rcon.Status.AddMapping(ParserRegex.GroupType.RConPing, 4);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 5);
        rcon.Status.AddMapping(ParserRegex.GroupType.RConName, 6);
        // basegame should not contain an absolute directory, but alas...
        rcon.OverrideDvarNameMapping.Add("fs_homepath", "fs_basegame");

        rconParser.IsOneLog = true;
        rconParser.Version = version;
        rconParser.GameName = Server.Game.IW5;
        eventParser.Version = version;
        eventParser.GameName = Server.Game.IW5;

        eventParser.Configuration.GameDirectory = "";
        eventParser.URLProtocolFormat = "plutonium://play/iw5mp/{{ip}}:{{port}}";

    }
}
