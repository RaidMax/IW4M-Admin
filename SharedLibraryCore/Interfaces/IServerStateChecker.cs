namespace SharedLibraryCore.Interfaces;

public interface IServerStateChecker : IDisposable
{
    /// <summary>
    /// The server this state checker is monitoring
    /// </summary>
    Server Server { get; }
    
    DateTime? LastActivity { get; }
    DateTime? FailStateDetectedAt { get; }
    
    /// <summary>
    /// Initialize the state checker for a specific server and subscribe to events
    /// </summary>
    void Initialize(Server server);
    
    /// <summary>
    /// Determines if players should be disconnected due to recent fail-state detection.
    /// This encapsulates the grace period logic which may vary by game.
    /// </summary>
    bool ShouldDisconnectPlayersOnFailState();
    
    /// <summary>
    /// Checks if the server is currently in a fail/error state
    /// </summary>
    bool IsInErrorState();
    
    /// <summary>
    /// Checks and updates fail-state status based on current client list
    /// </summary>
    bool CheckAndUpdateFailState();
    
    TimeSpan? GetFailStateDuration();
}

