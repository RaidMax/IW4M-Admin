using SharedLibraryCore.Interfaces;
using System.Globalization;

namespace IW4MAdmin.Application.EventParsers
{
    /// <summary>
    /// generic implementation of the IEventParserConfiguration
    /// allows script plugins to generate dynamic configurations
    /// </summary>
    sealed internal class DynamicEventParserConfiguration : IEventParserConfiguration
    {
        public string GameDirectory { get; set; }
        public ParserRegex Say { get; set; }
        public ParserRegex Join { get; set; }
        public ParserRegex Quit { get; set; }
        public ParserRegex Kill { get; set; }
        public ParserRegex Damage { get; set; }
        public ParserRegex Action { get; set; }
        public ParserRegex Time { get; set; }
        public NumberStyles GuidNumberStyle { get; set; } = NumberStyles.HexNumber;

        public DynamicEventParserConfiguration(IParserRegexFactory parserRegexFactory)
        {
            Say = parserRegexFactory.CreateParserRegex();
            Join = parserRegexFactory.CreateParserRegex();
            Quit = parserRegexFactory.CreateParserRegex();
            Kill = parserRegexFactory.CreateParserRegex();
            Damage = parserRegexFactory.CreateParserRegex();
            Action = parserRegexFactory.CreateParserRegex();
            Time = parserRegexFactory.CreateParserRegex();
        }
    }
}
