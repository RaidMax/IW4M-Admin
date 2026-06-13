using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Helpers;

/// <summary>
/// Helpers for script/plugin parsers to register themselves with the manager idempotently.
/// Registering by name (replacing any existing entry) guards against duplicate parsers when
/// the manager <see cref="Interfaces.Events.IManagementEventSubscriptions.Load"/> event fires
/// again on a restart.
/// </summary>
public static class ParserRegistrationExtensions
{
    /// <summary>
    /// Adds the RCon parser to the manager, replacing any existing parser with the same
    /// <see cref="IRConParser.Name"/>.
    /// </summary>
    public static void AddOrReplaceRConParser(this IManager manager, IRConParser parser)
    {
        var existing = manager.AdditionalRConParsers.FirstOrDefault(p => p.Name == parser.Name);
        if (existing is not null)
        {
            manager.AdditionalRConParsers.Remove(existing);
        }

        manager.AdditionalRConParsers.Add(parser);
    }

    /// <summary>
    /// Adds the event parser to the manager, replacing any existing parser with the same
    /// <see cref="IEventParser.Name"/>.
    /// </summary>
    public static void AddOrReplaceEventParser(this IManager manager, IEventParser parser)
    {
        var existing = manager.AdditionalEventParsers.FirstOrDefault(p => p.Name == parser.Name);
        if (existing is not null)
        {
            manager.AdditionalEventParsers.Remove(existing);
        }

        manager.AdditionalEventParsers.Add(parser);
    }
}
