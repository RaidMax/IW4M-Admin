using System;

namespace SharedLibraryCore.Events.Management;

public class NotifyAfterDelayCompleteEvent : ManagementEvent
{
    public Delegate Action { get; init; }
}
