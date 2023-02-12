using System;

namespace SharedLibraryCore.Events.Management;

public class NotifyAfterDelayRequestEvent : ManagementEvent
{
    public int DelayMs { get; init; }
    public Action Action { get; init; }
}
