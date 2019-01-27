using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.EventParsers
{
    class DynamicEventParserConfiguration : IEventParserConfiguration
    {
        public string GameDirectory { get; set; }

        public ParserRegex Say { get; set; }
        public ParserRegex Join { get; set; }
        public ParserRegex Quit { get; set; }
        public ParserRegex Kill { get; set; }
        public ParserRegex Damage { get; set; }

        public DynamicEventParserConfiguration()
        {
            Say = new ParserRegex();
            Join = new ParserRegex();
            Quit = new ParserRegex();
            Kill = new ParserRegex();
            Damage = new ParserRegex();
        }
    }
}
