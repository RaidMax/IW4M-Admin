using System;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Events;
using SharedLibraryCore.Events.Management;

namespace SharedLibraryCore.Interfaces.Events;

public interface IManagementEventSubscriptions
{
    /// <summary>
    /// Raised when <see cref="IManager"/> is loading
    /// </summary>
    static event Func<IManager, CancellationToken, Task> Load;
    
    /// <summary>
    /// Raised when <see cref="IManager"/> is restarting
    /// </summary>
    static event Func<IManager, CancellationToken, Task> Unload;

    /// <summary>
    /// Raised when client enters a tracked state
    /// <remarks>
    /// At this point, the client is not guaranteed to be allowed to play on the server.
    /// See <see cref="ClientStateAuthorized"/> for final state.
    /// </remarks>
    /// <value><see cref="ClientStateInitializeEvent"/></value>
    /// </summary>
    static event Func<ClientStateInitializeEvent, CancellationToken, Task> ClientStateInitialized;

    /// <summary>
    /// Raised when client enters an authorized state (valid data and no bans)
    /// <value><see cref="ClientStateAuthorizeEvent"/></value>
    /// </summary>
    static event Func<ClientStateAuthorizeEvent, CancellationToken, Task> ClientStateAuthorized;

    /// <summary>
    /// Raised when client is no longer tracked (unknown state)
    /// <remarks>At this point any references to the client should be dropped</remarks>
    /// <value><see cref="ClientStateDisposeEvent"/></value>
    /// </summary>
    static event Func<ClientStateDisposeEvent, CancellationToken, Task> ClientStateDisposed;

    /// <summary>
    /// Raised when a client receives a penalty
    /// <value><see cref="ClientPenaltyEvent"/></value>
    /// </summary>
    static event Func<ClientPenaltyEvent, CancellationToken, Task> ClientPenaltyAdministered;
    
    /// <summary>
    /// Raised when a client penalty is revoked (eg unflag/unban)
    /// <value><see cref="ClientPenaltyRevokeEvent"/></value>
    /// </summary>
    static event Func<ClientPenaltyRevokeEvent, CancellationToken, Task> ClientPenaltyRevoked;

    /// <summary>
    /// Raised when a client command is executed (after completion of the command)
    /// <value><see cref="ClientExecuteCommandEvent"/></value>
    /// </summary>
    static event Func<ClientExecuteCommandEvent, CancellationToken, Task> ClientCommandExecuted;

    /// <summary>
    /// Raised when a client's permission level changes
    /// <value><see cref="ClientPermissionChangeEvent"/></value>
    /// </summary>
    static event Func<ClientPermissionChangeEvent, CancellationToken, Task> ClientPermissionChanged;

    /// <summary>
    /// Raised when a client logs in to the webfront or ingame
    /// <value><see cref="LoginEvent"/></value>
    /// </summary>
    static event Func<LoginEvent, CancellationToken, Task> ClientLoggedIn;

    /// <summary>
    /// Raised when a client logs out of the webfront
    /// <value><see cref="LogoutEvent"/></value>
    /// </summary>
    static event Func<LogoutEvent, CancellationToken, Task> ClientLoggedOut;
    
    /// <summary>
    /// Raised when a client's persistent id (stats file marker) is received
    /// <value><see cref="ClientPersistentIdReceiveEvent"/></value>
    /// </summary>
    static event Func<ClientPersistentIdReceiveEvent, CancellationToken, Task> ClientPersistentIdReceived;

    static Task InvokeEventAsync(CoreEvent coreEvent, CancellationToken token)
    {
        return coreEvent switch
        {
            ClientStateInitializeEvent clientStateInitializeEvent => ClientStateInitialized?.InvokeAsync(
                clientStateInitializeEvent, token) ?? Task.CompletedTask,
            ClientStateDisposeEvent clientStateDisposedEvent => ClientStateDisposed?.InvokeAsync(
                clientStateDisposedEvent, token) ?? Task.CompletedTask,
            ClientStateAuthorizeEvent clientStateAuthorizeEvent => ClientStateAuthorized?.InvokeAsync(
                clientStateAuthorizeEvent, token) ?? Task.CompletedTask,
            ClientPenaltyRevokeEvent clientPenaltyRevokeEvent => ClientPenaltyRevoked?.InvokeAsync(
                clientPenaltyRevokeEvent, token) ?? Task.CompletedTask,
            ClientPenaltyEvent clientPenaltyEvent =>
                ClientPenaltyAdministered?.InvokeAsync(clientPenaltyEvent, token) ?? Task.CompletedTask,
            ClientPermissionChangeEvent clientPermissionChangeEvent => ClientPermissionChanged?.InvokeAsync(
                clientPermissionChangeEvent, token) ?? Task.CompletedTask,
            ClientExecuteCommandEvent clientExecuteCommandEvent => ClientCommandExecuted?.InvokeAsync(
                clientExecuteCommandEvent, token) ?? Task.CompletedTask,
            LogoutEvent logoutEvent => ClientLoggedOut?.InvokeAsync(logoutEvent, token) ?? Task.CompletedTask,
            LoginEvent loginEvent => ClientLoggedIn?.InvokeAsync(loginEvent, token) ?? Task.CompletedTask,
            ClientPersistentIdReceiveEvent clientPersistentIdReceiveEvent => ClientPersistentIdReceived?.InvokeAsync(
                clientPersistentIdReceiveEvent, token) ?? Task.CompletedTask,
            _ => Task.CompletedTask
        };
    }

    static Task InvokeLoadAsync(IManager manager, CancellationToken token) => Load?.InvokeAsync(manager, token) ?? Task.CompletedTask;
    static Task InvokeUnloadAsync(IManager manager, CancellationToken token) => Unload?.InvokeAsync(manager, token) ?? Task.CompletedTask;

    static void ClearEventInvocations()
    {
        Load = null;
        Unload = null;
        ClientStateInitialized = null;
        ClientStateAuthorized = null;
        ClientStateDisposed = null;
        ClientPenaltyAdministered = null;
        ClientPenaltyRevoked = null;
        ClientCommandExecuted = null;
        ClientPermissionChanged = null;
        ClientLoggedIn = null;
        ClientLoggedOut = null;
        ClientPersistentIdReceived = null;
    }
}
