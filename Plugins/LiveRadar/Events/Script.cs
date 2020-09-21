using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using EventGeneratorCallback = System.ValueTuple<string, string,
    System.Func<string, SharedLibraryCore.Interfaces.IEventParserConfiguration,
    SharedLibraryCore.GameEvent,
    SharedLibraryCore.GameEvent>>;

namespace LiveRadar.Events
{
    public class Script : IRegisterEvent
    {
        private const string EVENT_LIVERADAR = "LiveRadar";
        private EventGeneratorCallback LiveRadar()
        {
            return (EVENT_LIVERADAR, EVENT_LIVERADAR, (string eventLine, IEventParserConfiguration config, GameEvent autoEvent) =>
            {
                string[] lineSplit = eventLine.Split(";");

                autoEvent.Type = GameEvent.EventType.Other;
                autoEvent.Subtype = EVENT_LIVERADAR;
                autoEvent.Origin = new EFClient() { NetworkId = 0 };
                autoEvent.Extra = lineSplit[1]; // guid

                return autoEvent;
            }
            );
        }

        public IEnumerable<EventGeneratorCallback> Events => new[] { LiveRadar() };
    }
}
