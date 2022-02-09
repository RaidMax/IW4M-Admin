using System;
using System.Threading;
using Jint.Native;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc;

public class ScriptPluginTimerHelper : IScriptPluginTimerHelper
{
    private Timer _timer;
    private Action _actions;
    private Delegate _jsAction;
    private string _actionName;
    private const int DefaultDelay = 0;
    private const int DefaultInterval = 1000;
    private readonly ILogger _logger;
    private readonly ManualResetEventSlim _onRunningTick = new();
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

        _onRunningTick.Set();
        _timer ??= new Timer(callback => _actions(), null, delay, interval);
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
        _actions = OnTick;
    }

    private void ReleaseThreads()
    {
        _onRunningTick.Set();

        if (_onDependentAction?.CurrentCount != 0)
        {
            return;
        }

        _onDependentAction?.Release(1);
    }
    private void OnTick()
    {
        try
        {
            if (!_onRunningTick.IsSet)
            {
                _logger.LogDebug("Previous {OnTick} is still running, so we are skipping this one",
                    nameof(OnTick));
                return;
            }

            _onRunningTick.Reset();

            // the js engine is not thread safe so we need to ensure we're not executing OnTick and OnEventAsync simultaneously
            _onDependentAction?.WaitAsync().Wait();
            var start = DateTime.Now;
            _jsAction.DynamicInvoke(JsValue.Undefined, new[] { JsValue.Undefined });
            _logger.LogDebug("OnTick took {Time}ms", (DateTime.Now - start).TotalMilliseconds);
            ReleaseThreads();
        }

        catch (Exception ex) when (ex.InnerException is JavaScriptException jsex)
        {
            _logger.LogError(jsex,
                "Could not execute timer tick for script action {ActionName} [@{LocationInfo}]", _actionName,
                jsex.Location);
            ReleaseThreads();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not execute timer tick for script action {ActionName}", _actionName);
            _onRunningTick.Set();
            ReleaseThreads();
        }
    }

    public void SetDependency(SemaphoreSlim dependentSemaphore)
    {
        _onDependentAction = dependentSemaphore;
    }

    public bool IsRunning { get; private set; }
}
