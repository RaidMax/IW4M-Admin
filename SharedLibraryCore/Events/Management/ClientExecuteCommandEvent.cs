using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Events.Management;

public class ClientExecuteCommandEvent : ClientStateEvent
{
    public IManagerCommand Command { get; init; }
    public string CommandText { get; init; }
}
