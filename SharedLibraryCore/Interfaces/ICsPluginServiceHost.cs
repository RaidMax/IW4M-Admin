namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Manages the lifecycle of dynamically loaded .cs plugins with hot reload support.
/// </summary>
public interface ICsPluginServiceHost : IDisposable
{
    /// <summary>
    /// Starts watching the plugins directory for .cs file changes.
    /// Called after the application is initialized.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets all currently loaded .cs plugin instances.
    /// </summary>
    IEnumerable<IPluginV2> LoadedPlugins { get; }

    /// <summary>
    /// Gets the RCon parsers materialized from loaded .cs parser definitions.
    /// The manager projects these into its set of available parsers.
    /// </summary>
    IReadOnlyList<IRConParser> LoadedRConParsers { get; }

    /// <summary>
    /// Gets the event parsers materialized from loaded .cs parser definitions.
    /// The manager projects these into its set of available parsers.
    /// </summary>
    IReadOnlyList<IEventParser> LoadedEventParsers { get; }
}
