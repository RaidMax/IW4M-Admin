using System;
using System.Threading;

namespace SharedLibraryCore.Interfaces;

public interface IScriptPluginTimerHelper
{
    void Start(int delay, int interval);
    void Start(int interval);
    void Start();
    void Stop();
    void OnTick(Delegate action, string actionName);
    bool IsRunning { get; }
    void SetDependency(SemaphoreSlim dependentSemaphore);
}
