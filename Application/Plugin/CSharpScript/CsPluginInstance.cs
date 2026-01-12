using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharedLibraryCore.Interfaces;

#nullable enable

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Tracks the state of a single loaded .cs plugin.
/// </summary>
public class CsPluginInstance : IDisposable
{
    private string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public required CsPluginLoadContext LoadContext { get; init; }
    public required IServiceProvider PluginServiceProvider { get; init; }
    public required IPluginV2 Plugin { get; init; }
    private WeakReference? _contextWeakRef;

    public CsPluginInstance(string filePath)
    {
        FilePath = filePath;
        _contextWeakRef = new WeakReference(LoadContext);
    }

    /// <summary>
    /// Tracks commands registered by this plugin so they can be safely unregistered on unload.
    /// </summary>
    public List<IManagerCommand> RegisteredCommands { get; } = [];

    /// <summary>
    /// Waits for the AssemblyLoadContext to be garbage collected.
    /// </summary>
    /// <returns>True if the context was unloaded, false if references still exist</returns>
    public async Task<bool> WaitForUnloadAsync()
    {
        if (_contextWeakRef is null)
        {
            return true;
        }

        // Try multiple GC cycles to allow finalization
        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!_contextWeakRef.IsAlive)
            {
                _contextWeakRef = null;
                return true;
            }

            // Small delay between attempts
            await Task.Delay(100);
        }

        return false;
    }

    public void Dispose()
    {
        // Dispose the plugin if it implements IDisposable
        Plugin.Dispose();

        // Dispose the plugin service provider if it implements IDisposable
        if (PluginServiceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }

        // Initiate unload of the context
        LoadContext.Unload();
    }
}
