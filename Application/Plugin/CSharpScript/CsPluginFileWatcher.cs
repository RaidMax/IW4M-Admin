using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Event types for file changes.
/// </summary>
public enum FileEventType
{
    Created,
    Changed
}

/// <summary>
/// Watches the Plugins directory for .cs file changes with debouncing and per-file locking.
/// </summary>
public class CsPluginFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly ILogger<CsPluginFileWatcher> _logger;

    // Debounce file changes (editors often save multiple times, copy triggers both Created and Changed)
    private readonly Dictionary<string, DateTime> _lastChangeTime = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);

    // Per-file semaphores to prevent concurrent operations on the same file
    private readonly Dictionary<string, SemaphoreSlim> _fileOperationLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lockObject = new();

    /// <summary>
    /// Raised when a file is created or changed (after debouncing).
    /// </summary>
    public event Func<string, FileEventType, Task>? FileChanged;

    /// <summary>
    /// Raised when a file is deleted.
    /// </summary>
    public event Func<string, Task>? FileDeleted;

    /// <summary>
    /// Raised when a file is renamed.
    /// </summary>
    public event Func<string, string, Task>? FileRenamed;

    public CsPluginFileWatcher(ILogger<CsPluginFileWatcher> logger)
    {
        _logger = logger;

        if (!Directory.Exists(Utilities.PluginsDirectory))
        {
            Directory.CreateDirectory(Utilities.PluginsDirectory);
        }

        _watcher = new FileSystemWatcher(Utilities.PluginsDirectory, "*.cs")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = false,
            IncludeSubdirectories = false
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileCreated;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("Hot reload enabled - watching for .cs file changes");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        HandleFileEventAsync(e.FullPath, FileEventType.Changed);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        HandleFileEventAsync(e.FullPath, FileEventType.Created);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Plugin removed: {FileName}", e.Name);

        // Clean up debounce tracking for deleted file
        lock (_lockObject)
        {
            _lastChangeTime.Remove(e.FullPath);
        }

        _ = Task.Run(async () =>
        {
            var semaphore = GetOrCreateFileLock(e.FullPath);
            await semaphore.WaitAsync();
            try
            {
                if (FileDeleted != null)
                {
                    await FileDeleted(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file deletion {FileName}", e.Name);
            }
            finally
            {
                semaphore.Release();
            }
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation("Plugin renamed: {OldName} → {NewName}", e.OldName, e.Name);

        _ = Task.Run(async () =>
        {
            var oldSemaphore = GetOrCreateFileLock(e.OldFullPath);
            var newSemaphore = GetOrCreateFileLock(e.FullPath);

            await oldSemaphore.WaitAsync();
            try
            {
                if (FileRenamed != null)
                {
                    await FileRenamed(e.OldFullPath, e.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file rename {OldName} → {NewName}", e.OldName, e.Name);
            }
            finally
            {
                oldSemaphore.Release();
            }
        });
    }

    private void HandleFileEventAsync(string filePath, FileEventType eventType)
    {
        if (!ShouldProcessChange(filePath))
        {
            _logger.LogDebug("Skipping duplicate {EventType} event for {FileName}", eventType, Path.GetFileName(filePath));
            return;
        }

        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("Plugin file {EventType}: {FileName}", eventType, fileName);

        _ = Task.Run(async () =>
        {
            var semaphore = GetOrCreateFileLock(filePath);

            // Try to acquire the lock - if we can't get it immediately, another operation is in progress
            if (!await semaphore.WaitAsync(0))
            {
                _logger.LogDebug("Skipping {EventType} for {FileName} - operation already in progress", eventType, fileName);
                return;
            }

            try
            {
                // Let file system settle
                await Task.Delay(200);

                // Check if file still exists (might have been deleted)
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("File no longer exists: {FileName}", fileName);
                    return;
                }

                if (FileChanged != null)
                {
                    await FileChanged(filePath, eventType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling {EventType} for plugin {FileName}", eventType, fileName);
            }
            finally
            {
                semaphore.Release();
            }
        });
    }

    private SemaphoreSlim GetOrCreateFileLock(string filePath)
    {
        lock (_lockObject)
        {
            if (_fileOperationLocks.TryGetValue(filePath, out var semaphore))
            {
                try
                {
                    _ = semaphore.CurrentCount;
                    return semaphore;
                }
                catch (ObjectDisposedException)
                {
                    _fileOperationLocks.Remove(filePath);
                }
            }

            semaphore = new SemaphoreSlim(1, 1);
            _fileOperationLocks[filePath] = semaphore;
            return semaphore;
        }
    }

    private bool ShouldProcessChange(string filePath)
    {
        var now = DateTime.UtcNow;

        lock (_lockObject)
        {
            if (_lastChangeTime.TryGetValue(filePath, out var lastTime))
            {
                if (now - lastTime < _debounceInterval)
                {
                    return false;
                }
            }

            _lastChangeTime[filePath] = now;
        }

        return true;
    }

    /// <summary>
    /// Cleans up tracking state for a file path.
    /// </summary>
    public void CleanupFilePath(string filePath)
    {
        lock (_lockObject)
        {
            if (_fileOperationLocks.TryGetValue(filePath, out var semaphore))
            {
                try
                {
                    semaphore.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed
                }

                _fileOperationLocks.Remove(filePath);
            }

            _lastChangeTime.Remove(filePath);
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        foreach (var semaphore in _fileOperationLocks.Values)
        {
            semaphore.Dispose();
        }

        _fileOperationLocks.Clear();
    }
}
