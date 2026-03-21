using SharedLibraryCore.Interfaces.Events;

namespace SharedLibraryCore.Events.Game;

public class LatencyProbeScriptEvent : GameScriptEvent, IGameScriptEvent
{
    public string ProbeId { get; set; }

    // Explicit interface implementation so it doesn't appear as a DeclaredOnly property
    // (which would break positional argument parsing in ParseArguments)
    string IGameScriptEvent.EventName => "LP";
}
