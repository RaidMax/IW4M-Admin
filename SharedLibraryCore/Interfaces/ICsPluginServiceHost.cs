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
}
