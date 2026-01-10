using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Tracks the state of a single loaded .cs plugin.
/// </summary>
internal class CsPluginInstance(string filePath) : IDisposable
{
    public string FilePath { get; } = filePath;
    public string FileName => Path.GetFileName(FilePath);
    public CsPluginLoadContext? LoadContext { get; set; }
    public WeakReference? ContextWeakRef { get; set; }
    public IPluginV2? Plugin { get; set; }
    public IServiceProvider? PluginServiceProvider { get; set; }
    
    /// <summary>
    /// Tracks commands registered by this plugin so they can be safely unregistered on unload.
    /// </summary>
    public List<IManagerCommand> RegisteredCommands { get; } = new();

    /// <summary>
    /// Waits for the AssemblyLoadContext to be garbage collected.
    /// </summary>
    /// <returns>True if the context was unloaded, false if references still exist</returns>
    public async Task<bool> WaitForUnloadAsync()
    {
        if (ContextWeakRef == null)
        {
            return true;
        }

        // Try multiple GC cycles to allow finalization
        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!ContextWeakRef.IsAlive)
            {
                ContextWeakRef = null;
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
        Plugin?.Dispose();
        Plugin = null;

        // Dispose the plugin service provider if it implements IDisposable
        if (PluginServiceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }

        PluginServiceProvider = null;

        // Initiate unload of the context
        LoadContext?.Unload();
        LoadContext = null;
    }
}
