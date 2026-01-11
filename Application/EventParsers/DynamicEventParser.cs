using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Application.EventParsers
{
    /// <summary>
    /// empty generic implementation of the IEventParserConfiguration
    /// allows script plugins to generate dynamic event parsers
    /// </summary>
    internal sealed class DynamicEventParser(
        IParserRegexFactory parserRegexFactory,
        ILogger<DynamicEventParser> logger,
        ApplicationConfiguration appConfig,
        IGameScriptEventFactory gameScriptEventFactory)
        : BaseEventParser(parserRegexFactory, logger, appConfig, gameScriptEventFactory);
}
