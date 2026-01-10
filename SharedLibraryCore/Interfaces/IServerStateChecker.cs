using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces;

public interface IServerStateChecker
{
    DateTime? LastActivity { get; }
    DateTime? FailStateDetectedAt { get; }
    void RecordActivity();
    bool IsErrorState(int nonBotPlayerCount);
    void ProcessPlayerUpdates(IEnumerable<EFClient> updatedClients, IEnumerable<EFClient> polledClients);
    bool CheckAndUpdateFailState(List<EFClient> currentClients);
    TimeSpan? GetFailStateDuration();
}
