using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using EventGeneratorCallback = System.ValueTuple<string, string, 
    System.Func<string, SharedLibraryCore.Interfaces.IEventParserConfiguration,
    SharedLibraryCore.GameEvent, 
    SharedLibraryCore.GameEvent>>;

namespace IW4MAdmin.Plugins.Stats.Events
{
    public class Script : IRegisterEvent
    {
        private const string EVENT_SCRIPTKILL = "ScriptKill";
        private const string EVENT_SCRIPTDAMAGE = "ScriptDamage";
        private const string EVENT_JOINTEAM = "JoinTeam";

        /// <summary>
        /// this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
        /// </summary>
        /// <returns></returns>
        private EventGeneratorCallback ScriptKill()
        {
            return (EVENT_SCRIPTKILL, EVENT_SCRIPTKILL, (string eventLine, IEventParserConfiguration config, GameEvent autoEvent) =>
            {
                string[] lineSplit = eventLine.Split(";");
                long originId = lineSplit[1].ConvertGuidToLong(config.GuidNumberStyle, 1);
                long targetId = lineSplit[2].ConvertGuidToLong(config.GuidNumberStyle, 1);

                autoEvent.Type = GameEvent.EventType.ScriptKill;
                autoEvent.Origin = new EFClient() { NetworkId = originId };
                autoEvent.Target = new EFClient() { NetworkId = targetId };
                autoEvent.RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target;
                autoEvent.GameTime = autoEvent.GameTime;

                return autoEvent;
            }
            );
        }

        /// <summary>
        /// this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
        /// </summary>
        /// <returns></returns>
        private EventGeneratorCallback ScriptDamage()
        {
            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            return (EVENT_SCRIPTDAMAGE, EVENT_SCRIPTDAMAGE, (string eventLine, IEventParserConfiguration config, GameEvent autoEvent) =>
            {
                string[] lineSplit = eventLine.Split(";");
                long originId = lineSplit[1].ConvertGuidToLong(config.GuidNumberStyle, 1);
                long targetId = lineSplit[2].ConvertGuidToLong(config.GuidNumberStyle, 1);

                autoEvent.Type = GameEvent.EventType.ScriptDamage;
                autoEvent.Origin = new EFClient() { NetworkId = originId };
                autoEvent.Target = new EFClient() { NetworkId = targetId };
                autoEvent.RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target;

                return autoEvent;
            }
            );
        }

        /// <summary>
        /// this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
        /// </summary>
        /// <returns></returns>
        private EventGeneratorCallback JoinTeam()
        {
            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            return (EVENT_JOINTEAM, EVENT_JOINTEAM, (string eventLine, IEventParserConfiguration config, GameEvent autoEvent) =>
            {
                string[] lineSplit = eventLine.Split(";");
                long originId = lineSplit[1].ConvertGuidToLong(config.GuidNumberStyle, 1);
                long targetId = lineSplit[2].ConvertGuidToLong(config.GuidNumberStyle, 1);

                autoEvent.Type = GameEvent.EventType.JoinTeam;
                autoEvent.Origin = new EFClient() { NetworkId = lineSplit[1].ConvertGuidToLong(config.GuidNumberStyle) };
                autoEvent.RequiredEntity = GameEvent.EventRequiredEntity.Target;

                return autoEvent;
            }
            );
        }

        public IEnumerable<EventGeneratorCallback> Events =>
            new[]
            {
                ScriptKill(),
                ScriptDamage(),
                JoinTeam()
            };
    }
}
