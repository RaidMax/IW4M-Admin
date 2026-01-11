using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces;

public interface IServerStateChecker
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
    
    void RecordActivity();
    bool IsErrorState(int nonBotPlayerCount);
    void ProcessPlayerUpdates(IEnumerable<EFClient> updatedClients, IEnumerable<EFClient> polledClients);
    bool CheckAndUpdateFailState(List<EFClient> currentClients);
    TimeSpan? GetFailStateDuration();
}

