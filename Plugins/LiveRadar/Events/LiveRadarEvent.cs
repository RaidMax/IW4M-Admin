using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using SharedLibraryCore.Events.Game;
using EventGeneratorCallback = System.ValueTuple<string, string,
    System.Func<string, SharedLibraryCore.Interfaces.IEventParserConfiguration,
        SharedLibraryCore.GameEvent,
        SharedLibraryCore.GameEvent>>;

namespace IW4MAdmin.Plugins.LiveRadar.Events;

public class Script : IRegisterEvent
{
    private const string EventLiveRadar = "LiveRadar";

    private EventGeneratorCallback LiveRadar()
    {
        return (EventLiveRadar, EventLiveRadar, (eventLine, _, _) =>
                {
                    var radarEvent = new LiveRadarEvent
                    {
                        Type = GameEvent.EventType.Other,
                        Subtype = EventLiveRadar,
                        Origin = new EFClient { NetworkId = 0 },
                        ScriptData = eventLine
                    };
                    return radarEvent;
                }
            );
    }

    public IEnumerable<EventGeneratorCallback> Events => new[] { LiveRadar() };
}

public class LiveRadarEvent : GameScriptEvent
{
}
