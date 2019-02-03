using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.EventParsers
{
    /// <summary>
    /// generic implementation of the IEventParserConfiguration
    /// allows script plugins to generate dynamic configurations
    /// </summary>
    sealed internal class DynamicEventParserConfiguration : IEventParserConfiguration
    {
        public string GameDirectory { get; set; }
        public ParserRegex Say { get; set; } = new ParserRegex();
        public ParserRegex Join { get; set; } = new ParserRegex();
        public ParserRegex Quit { get; set; } = new ParserRegex();
        public ParserRegex Kill { get; set; } = new ParserRegex();
        public ParserRegex Damage { get; set; } = new ParserRegex();
        public ParserRegex Action { get; set; } = new ParserRegex();
    }
}
