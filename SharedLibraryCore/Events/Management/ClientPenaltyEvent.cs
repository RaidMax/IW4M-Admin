using Data.Models;
using Data.Models.Client;

namespace SharedLibraryCore.Events.Management;

public class ClientPenaltyEvent : ManagementEvent
{
    public EFClient Client { get; init; }
    public EFPenalty Penalty { get; init; }
}
