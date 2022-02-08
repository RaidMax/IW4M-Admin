using System;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
using System.Collections.Generic;
using System.Globalization;
using SharedLibraryCore.Formatting;

namespace IW4MAdmin.Application.RConParsers
{
    /// <summary>
    /// generic implementation of the IRConParserConfiguration
    /// allows script plugins to generate dynamic RCon configurations
    /// </summary>
    public class DynamicRConParserConfiguration : IRConParserConfiguration
    {
        public CommandPrefix CommandPrefixes { get; set; }
        public ParserRegex Status { get; set; }
        public ParserRegex MapStatus { get; set; }
        public ParserRegex GametypeStatus { get; set; }
        public ParserRegex HostnameStatus { get; set; }
        public ParserRegex MaxPlayersStatus { get; set; }
        public ParserRegex Dvar { get; set; }
        public ParserRegex StatusHeader { get; set; }
        public string ServerNotRunningResponse { get; set; }
        public bool WaitForResponse { get; set; } = true;
        public NumberStyles GuidNumberStyle { get; set; } = NumberStyles.HexNumber;
        public IDictionary<string, string> OverrideDvarNameMapping { get; set; } = new Dictionary<string, string>();
        public IDictionary<string, string> DefaultDvarValues { get; set; } = new Dictionary<string, string>();
        public int NoticeMaximumLines { get; set; } = 8;
        public int NoticeMaxCharactersPerLine { get; set; } = 50;
        public string NoticeLineSeparator { get; set; } = Environment.NewLine;
        public int? DefaultRConPort { get; set; }
        public string DefaultInstallationDirectoryHint { get; set; }
        public short FloodProtectInterval { get; set; } = 750;

        public ColorCodeMapping ColorCodeMapping { get; set; } = new ColorCodeMapping
        {
            // this is the default mapping (IW4), but can be overridden as needed in the parsers
            {ColorCodes.Black.ToString(), "^0"},
            {ColorCodes.Red.ToString(), "^1"},
            {ColorCodes.Green.ToString(), "^2"},
            {ColorCodes.Yellow.ToString(), "^3"},
            {ColorCodes.Blue.ToString(), "^4"},
            {ColorCodes.Cyan.ToString(), "^5"},
            {ColorCodes.Pink.ToString(), "^6"},
            {ColorCodes.White.ToString(), "^7"},
            {ColorCodes.Map.ToString(), "^8"},
            {ColorCodes.Grey.ToString(), "^9"},
            {ColorCodes.Wildcard.ToString(), ":^"},
        };

        public DynamicRConParserConfiguration(IParserRegexFactory parserRegexFactory)
        {
            Status = parserRegexFactory.CreateParserRegex();
            MapStatus = parserRegexFactory.CreateParserRegex();
            GametypeStatus = parserRegexFactory.CreateParserRegex();
            Dvar = parserRegexFactory.CreateParserRegex();
            StatusHeader = parserRegexFactory.CreateParserRegex();
            HostnameStatus = parserRegexFactory.CreateParserRegex();
            MaxPlayersStatus = parserRegexFactory.CreateParserRegex();
        }
    }
}
