using System;
using System.Collections.Generic;
using System.IO;
using SharedLibraryCore;

namespace IW4MAdmin.Application.IO;

public sealed class ConfigurationWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, Action<string>> _registeredActions = new();

    public ConfigurationWatcher()
    {
        _watcher = new FileSystemWatcher
        {
            Path = Path.Join(Utilities.OperatingDirectory, "Configuration"),
            Filter = "*.json",
            NotifyFilter = NotifyFilters.LastWrite
        };

        _watcher.Changed += WatcherOnChanged;
    }

    public void Dispose()
    {
        _watcher.Changed -= WatcherOnChanged;
        _watcher.Dispose();
    }

    public void Register(string fileName, Action<string> fileUpdated)
    {
        _registeredActions.TryAdd(fileName, fileUpdated);
    }

    public void Unregister(string fileName)
    {
        _registeredActions.Remove(fileName);
    }

    public void Enable()
    {
        _watcher.EnableRaisingEvents = true;
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs eventArgs)
    {
        if (!_registeredActions.TryGetValue(eventArgs.FullPath, out var value) ||
            eventArgs.ChangeType != WatcherChangeTypes.Changed ||
            new FileInfo(eventArgs.FullPath).Length == 0)
        {
            return;
        }

        value.Invoke(eventArgs.FullPath);
    }
}
