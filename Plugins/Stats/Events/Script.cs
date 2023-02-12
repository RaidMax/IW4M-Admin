using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using SharedLibraryCore.Events.Game;
using EventGeneratorCallback = System.ValueTuple<string, string,
    System.Func<string, SharedLibraryCore.Interfaces.IEventParserConfiguration,
        SharedLibraryCore.GameEvent,
        SharedLibraryCore.GameEvent>>;

namespace IW4MAdmin.Plugins.Stats.Events
{
    public class Script : IRegisterEvent
    {
        private const string EventScriptKill = "ScriptKill";
        private const string EventScriptDamage = "ScriptDamage";

        /// <summary>
        /// this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
        /// </summary>
        /// <returns></returns>
        private static EventGeneratorCallback ScriptKill()
        {
            return (EventScriptKill, EventScriptKill,
                    (eventLine, config, autoEvent) =>
                    {
                        var lineSplit = eventLine.Split(";");

                        if (lineSplit[1].IsBotGuid() || lineSplit[2].IsBotGuid())
                        {
                            return autoEvent;
                        }

                        var originId = lineSplit[1].ConvertGuidToLong(config.GuidNumberStyle, 1);
                        var targetId = lineSplit[2].ConvertGuidToLong(config.GuidNumberStyle, 1);

                        var anticheatEvent = new AntiCheatDamageEvent
                        {
                            ScriptData = eventLine,
                            Type = GameEvent.EventType.ScriptKill,
                            Origin = new EFClient { NetworkId = originId },
                            Target = new EFClient { NetworkId = targetId },
                            RequiredEntity =
                                GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                            GameTime = autoEvent.GameTime,
                            IsKill = true
                        };

                        return anticheatEvent;
                    }
                );
        }

        /// <summary>
        /// this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
        /// </summary>
        /// <returns></returns>
        public EventGeneratorCallback ScriptDamage()
        {
            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            return (EventScriptDamage, EventScriptDamage,
                    (eventLine, config, autoEvent) =>
                    {
                        var lineSplit = eventLine.Split(";");

                        if (lineSplit[1].IsBotGuid() || lineSplit[2].IsBotGuid())
                        {
                            return autoEvent;
                        }

                        var originId = lineSplit[1].ConvertGuidToLong(config.GuidNumberStyle, 1);
                        var targetId = lineSplit[2].ConvertGuidToLong(config.GuidNumberStyle, 1);

                        var anticheatEvent = new AntiCheatDamageEvent
                        {
                            ScriptData = eventLine,
                            Type = GameEvent.EventType.ScriptDamage,
                            Origin = new EFClient { NetworkId = originId },
                            Target = new EFClient { NetworkId = targetId },
                            RequiredEntity =
                                GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                            GameTime = autoEvent.GameTime
                        };

                        return anticheatEvent;
                    }
                );
        }

        public IEnumerable<EventGeneratorCallback> Events =>
            new[]
            {
                ScriptKill(),
                ScriptDamage()
            };
    }

    public class AntiCheatDamageEvent : GameScriptEvent
    {
        public bool IsKill { get; init; }
    }
}
