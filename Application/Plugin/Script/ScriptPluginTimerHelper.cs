using System;
using System.Threading;
using Jint.Native;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Plugin.Script;

[Obsolete("This architecture is superseded by the request notify delay architecture")]
public class ScriptPluginTimerHelper : IScriptPluginTimerHelper
{
    private Timer _timer;
    private Action _actions;
    private Delegate _jsAction;
    private string _actionName;
    private int _interval = DefaultInterval;
    private long _waitingCount;
    private const int DefaultDelay = 0;
    private const int DefaultInterval = 1000;
    private const int MaxWaiting = 10;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _onRunningTick = new(1, 1);
    private SemaphoreSlim _onDependentAction;

    public ScriptPluginTimerHelper(ILogger<ScriptPluginTimerHelper> logger)
    {
        _logger = logger;
    }

    ~ScriptPluginTimerHelper()
    {
        if (_timer != null)
        {
            Stop();
        }

        _onRunningTick.Dispose();
    }

    public void Start(int delay, int interval)
    {
        if (_actions is null)
        {
            throw new InvalidOperationException("Timer action must be defined before starting");
        }

        if (delay < 0)
        {
            throw new ArgumentException("Timer delay must be >= 0");
        }

        if (interval < 20)
        {
            throw new ArgumentException("Timer interval must be at least 20ms");
        }

        Stop();

        _logger.LogDebug("Starting script timer...");

        _timer ??= new Timer(callback => _actions(), null, delay, interval);
        _interval = interval;
        IsRunning = true;
    }

    public void Start(int interval)
    {
        Start(DefaultDelay, interval);
    }

    public void Start()
    {
        Start(DefaultDelay, DefaultInterval);
    }

    public void Stop()
    {
        if (_timer == null)
        {
            return;
        }

        _logger.LogDebug("Stopping script timer...");
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _timer.Dispose();
        _timer = null;
        IsRunning = false;
    }

    public void OnTick(Delegate action, string actionName)
    {
        if (string.IsNullOrEmpty(actionName))
        {
            throw new ArgumentException("actionName must be provided", nameof(actionName));
        }

        if (action is null)
        {
            throw new ArgumentException("action must be provided", nameof(action));
        }

        _logger.LogDebug("Adding new action with name {ActionName}", actionName);

        _jsAction = action;
        _actionName = actionName;
        _actions = OnTickInternal;
    }

    private void ReleaseThreads(bool releaseOnRunning, bool releaseOnDependent)
    {
        if (releaseOnRunning && _onRunningTick.CurrentCount == 0)
        {
            _logger.LogDebug("-Releasing OnRunning for timer");
            _onRunningTick.Release(1);
        }

        if (releaseOnDependent && _onDependentAction?.CurrentCount == 0)
        {
            _onDependentAction?.Release(1);
        }
    }

    private async void OnTickInternal()
    {
        var releaseOnRunning = false;
        var releaseOnDependent = false;

        try
        {
            try
            {
                if (Interlocked.Read(ref _waitingCount) > MaxWaiting)
                {
                    _logger.LogWarning("Reached max number of waiting count ({WaitingCount}) for {OnTick}",
                        _waitingCount, nameof(OnTickInternal));
                    return;
                }

                Interlocked.Increment(ref _waitingCount);
                using var tokenSource1 = new CancellationTokenSource();
                tokenSource1.CancelAfter(TimeSpan.FromMilliseconds(_interval));
                await _onRunningTick.WaitAsync(tokenSource1.Token);
                releaseOnRunning = true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Previous {OnTick} is still running, so we are skipping this one",
                    nameof(OnTickInternal));
                return;
            }

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                // the js engine is not thread safe so we need to ensure we're not executing OnTick and OnEventAsync simultaneously
                if (_onDependentAction is not null)
                {
                    await _onDependentAction.WaitAsync(tokenSource.Token);
                    releaseOnDependent = true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dependent action did not release in allotted time so we are cancelling this tick");
                return;
            }

            _logger.LogDebug("+Running OnTick for timer");
            var start = DateTime.Now;
            _jsAction.DynamicInvoke(JsValue.Undefined, new[] { JsValue.Undefined });
            _logger.LogDebug("OnTick took {Time}ms", (DateTime.Now - start).TotalMilliseconds);
        }
        catch (Exception ex) when (ex.InnerException is JavaScriptException jsx)
        {
            _logger.LogError(jsx,
                "Could not execute timer tick for script action {ActionName} [{@LocationInfo}] [{@StackTrace}]",
                _actionName,
                jsx.Location, jsx.JavaScriptStackTrace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not execute timer tick for script action {ActionName}", _actionName);
        }
        finally
        {
            ReleaseThreads(releaseOnRunning, releaseOnDependent);
            Interlocked.Decrement(ref _waitingCount);
        }
    }

    public void SetDependency(SemaphoreSlim dependentSemaphore)
    {
        _onDependentAction = dependentSemaphore;
    }

    public bool IsRunning { get; private set; }
}
