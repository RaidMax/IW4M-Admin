using System;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Events;
using SharedLibraryCore.Events.Server;

namespace SharedLibraryCore.Interfaces.Events;

public interface IGameServerEventSubscriptions
{
    /// <summary>
    /// Raised when IW4MAdmin starts monitoring a game server
    /// <value><see cref="MonitorStartEvent"/></value>
    /// </summary>
    static event Func<MonitorStartEvent, CancellationToken, Task> MonitoringStarted;
    
    /// <summary>
    /// Raised when IW4MAdmin stops monitoring a game server
    /// <value><see cref="MonitorStopEvent"/></value>
    /// </summary>
    static event Func<MonitorStopEvent, CancellationToken, Task> MonitoringStopped;
    
    /// <summary>
    /// Raised when communication was interrupted with a game server
    /// <value><see cref="ConnectionInterruptEvent"/></value>
    /// </summary>
    static event Func<ConnectionInterruptEvent, CancellationToken, Task> ConnectionInterrupted;
    
    /// <summary>
    /// Raised when communication was resumed with a game server
    /// <value><see cref="ConnectionRestoreEvent"/></value>
    /// </summary>
    static event Func<ConnectionRestoreEvent, CancellationToken, Task> ConnectionRestored;
    
    /// <summary>
    /// Raised when updated client data was received from a game server
    /// <value><see cref="ClientDataUpdateEvent"/></value>
    /// </summary>
    static event Func<ClientDataUpdateEvent, CancellationToken, Task> ClientDataUpdated;
    
    /// <summary>
    /// Raised when a command is requested to be executed on a game server
    /// </summary>
    static event Func<ServerCommandRequestExecuteEvent, CancellationToken, Task> ServerCommandExecuteRequested;
    
    /// <summary>
    /// Raised when a command was executed on a game server
    /// <value><see cref="ServerCommandExecuteEvent"/></value>
    /// </summary>
    static event Func<ServerCommandExecuteEvent, CancellationToken, Task> ServerCommandExecuted;
    
    /// <summary>
    /// Raised when a server value is requested for a game server
    /// <value><see cref="ServerValueRequestEvent"/></value>
    /// </summary>
    static event Func<ServerValueRequestEvent, CancellationToken, Task> ServerValueRequested;
    
    /// <summary>
    /// Raised when a server value was received from a game server (success or fail)
    /// <value><see cref="ServerValueReceiveEvent"/></value>
    /// </summary>
    static event Func<ServerValueReceiveEvent, CancellationToken, Task> ServerValueReceived;

    /// <summary>
    /// Raised when a request to set a server value on a game server is received
    /// <value><see cref="ServerValueSetRequestEvent"/></value>
    /// </summary>
    static event Func<ServerValueSetRequestEvent, CancellationToken, Task> ServerValueSetRequested;

    /// <summary>
    /// Raised when a setting server value on a game server is completed (success or fail)
    /// <value><see cref="ServerValueSetRequestEvent"/></value>
    /// </summary>
    static event Func<ServerValueSetCompleteEvent, CancellationToken, Task> ServerValueSetCompleted;

    static Task InvokeEventAsync(CoreEvent coreEvent, CancellationToken token)
    {
        return coreEvent switch
        {
            MonitorStartEvent monitoringStartEvent => MonitoringStarted?.InvokeAsync(monitoringStartEvent, token) ?? Task.CompletedTask,
            MonitorStopEvent monitorStopEvent => MonitoringStopped?.InvokeAsync(monitorStopEvent, CancellationToken.None) ?? Task.CompletedTask,
            ConnectionInterruptEvent connectionInterruptEvent => ConnectionInterrupted?.InvokeAsync(connectionInterruptEvent, token) ?? Task.CompletedTask,
            ConnectionRestoreEvent connectionRestoreEvent => ConnectionRestored?.InvokeAsync(connectionRestoreEvent, token) ?? Task.CompletedTask,
            ClientDataUpdateEvent clientDataUpdateEvent => ClientDataUpdated?.InvokeAsync(clientDataUpdateEvent, token) ?? Task.CompletedTask,
            ServerCommandRequestExecuteEvent serverCommandRequestExecuteEvent => ServerCommandExecuteRequested?.InvokeAsync(serverCommandRequestExecuteEvent, token) ?? Task.CompletedTask,
            ServerCommandExecuteEvent dataReceiveEvent => ServerCommandExecuted?.InvokeAsync(dataReceiveEvent, token) ?? Task.CompletedTask,
            ServerValueRequestEvent serverValueRequestEvent => ServerValueRequested?.InvokeAsync(serverValueRequestEvent, token) ?? Task.CompletedTask,
            ServerValueReceiveEvent serverValueReceiveEvent => ServerValueReceived?.InvokeAsync(serverValueReceiveEvent, token) ?? Task.CompletedTask,
            ServerValueSetRequestEvent serverValueSetRequestEvent => ServerValueSetRequested?.InvokeAsync(serverValueSetRequestEvent, token) ?? Task.CompletedTask,
            ServerValueSetCompleteEvent serverValueSetCompleteEvent => ServerValueSetCompleted?.InvokeAsync(serverValueSetCompleteEvent, token) ?? Task.CompletedTask,
            _ => Task.CompletedTask
        };
    }

    static void ClearEventInvocations()
    {
        MonitoringStarted = null;
        MonitoringStopped = null;
        ConnectionInterrupted = null;
        ConnectionRestored = null;
        ClientDataUpdated = null;
        ServerCommandExecuteRequested = null;
        ServerCommandExecuted = null;
        ServerValueReceived = null;
        ServerValueRequested = null;
        ServerValueSetRequested = null;
        ServerValueSetCompleted = null;
    }
}
