using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;

#nullable enable

namespace IW4MAdmin.Application.Plugin.CSharpScript;

public class CsPluginFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly ILogger<CsPluginFileWatcher> _logger;
    private readonly Channel<FileSystemEventArgs> _eventChannel;
    private readonly CancellationTokenSource _cts;
    private Task? _processingTask;

    private readonly ConcurrentDictionary<string, DateTime> _debounceTracker = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(500);

    public event Func<string, Task>? FileChanged;
    public event Func<string, Task>? FileDeleted;
    public event Func<string, string, Task>? FileRenamed;

    public CsPluginFileWatcher(ILogger<CsPluginFileWatcher> logger)
    {
        _logger = logger;
        _cts = new CancellationTokenSource();

        _eventChannel = Channel.CreateUnbounded<FileSystemEventArgs>();

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

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
    }

    public void Start()
    {
        _processingTask = Task.Run(() => ProcessEventsAsync(_cts.Token));
        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("Hot reload enabled - watching for .cs file changes");
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _eventChannel.Writer.TryWrite(e);
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        try
        {
            while (await _eventChannel.Reader.WaitToReadAsync(ct))
            {
                while (_eventChannel.Reader.TryRead(out var e))
                {
                    await HandleEventSafeAsync(e, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in plugin file watcher loop");
        }
    }

    private async Task HandleEventSafeAsync(FileSystemEventArgs eventArg, CancellationToken token)
    {
        try
        {
            var path = eventArg.FullPath;

            var now = DateTime.UtcNow;
            if (_debounceTracker.TryGetValue(path, out var lastTime))
            {
                if (now - lastTime < DebounceWindow)
                {
                    _debounceTracker[path] = now;
                    return;
                }
            }

            _debounceTracker[path] = now;

            switch (eventArg.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed:
                    if (await WaitForFileAccessAsync(path, token))
                    {
                        if (FileChanged != null)
                            await FileChanged(path);
                    }

                    break;
                case WatcherChangeTypes.Deleted:
                    _debounceTracker.TryRemove(path, out _);
                    if (FileDeleted != null)
                        await FileDeleted(path);
                    break;
                case WatcherChangeTypes.Renamed:
                    if (eventArg is RenamedEventArgs re)
                    {
                        _debounceTracker.TryRemove(re.OldFullPath, out _);
                        if (FileRenamed != null)
                            await FileRenamed(re.OldFullPath, re.FullPath);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file event for {Path}", eventArg.FullPath);
        }
    }

    private async Task<bool> WaitForFileAccessAsync(string filePath, CancellationToken ct)
    {
        const int maxRetries = 10;
        const int delayMs = 100;

        for (var i = 0; i < maxRetries; i++)
        {
            if (ct.IsCancellationRequested)
                return false;
            if (!File.Exists(filePath))
                return false;

            try
            {
                // Try to open the file for reading. If this succeeds, the writer is done.
                await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                // File is still locked by the writer
                await Task.Delay(delayMs, ct);
            }
        }

        _logger.LogWarning("Timed out waiting for file access: {Path}", filePath);
        return false;
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        _processingTask?.Wait();
    }
}
