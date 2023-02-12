using System.Collections.Generic;

namespace SharedLibraryCore.Events.Game;

public class MatchStartEvent : GameEventV2
{
    public IReadOnlyDictionary<string, string> SessionData { get; init; } = new Dictionary<string, string>();
}
