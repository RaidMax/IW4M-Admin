using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.EventParsers
{
    /// <summary>
    /// empty generic implementation of the IEventParserConfiguration
    /// allows script plugins to generate dynamic event parsers
    /// </summary>
    sealed internal class DynamicEventParser : BaseEventParser
    {
        public DynamicEventParser(IParserRegexFactory parserRegexFactory, ILogger logger) : base(parserRegexFactory, logger)
        {
        }
    }
}
