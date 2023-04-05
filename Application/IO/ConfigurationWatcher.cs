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
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher.Changed -= WatcherOnChanged;
        _watcher.Dispose();
    }

    public void Register(string fileName, Action<string> fileUpdated)
    {
        if (_registeredActions.ContainsKey(fileName))
        {
            return;
        }

        _registeredActions.Add(fileName, fileUpdated);
    }

    public void Unregister(string fileName)
    {
        if (_registeredActions.ContainsKey(fileName))
        {
            _registeredActions.Remove(fileName);
        }
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs eventArgs)
    {
        if (!_registeredActions.ContainsKey(eventArgs.FullPath) || eventArgs.ChangeType != WatcherChangeTypes.Changed ||
            new FileInfo(eventArgs.FullPath).Length == 0)
        {
            return;
        }

        _registeredActions[eventArgs.FullPath].Invoke(eventArgs.FullPath);
    }
}
