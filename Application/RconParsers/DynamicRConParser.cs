using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.RconParsers
{
    /// <summary>
    /// empty implementation of the IW4RConParser
    /// allows script plugins to generate dynamic RCon parsers
    /// </summary>
    sealed internal class DynamicRConParser : BaseRConParser
    {
        public DynamicRConParser(ILogger<BaseRConParser> logger, IParserRegexFactory parserRegexFactory) : base(logger, parserRegexFactory)
        {
        }
    }
}
