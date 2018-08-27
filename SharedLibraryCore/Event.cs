using System;
using System.Threading;
using SharedLibraryCore.Objects;

namespace SharedLibraryCore
{
    public class GameEvent
    {
        public enum EventType
        {
            Unknown,

            // events "generated" by the server
            Start,
            Stop,
            Connect,
            Join,
            Quit,
            Disconnect,
            MapEnd,
            MapChange,

            // events "generated" by clients    
            Say,
            Report,
            Flag,
            Unflag,
            Kick,
            TempBan,
            Ban,
            Command,

            // events "generated" by IW4MAdmin
            Broadcast,
            Tell,

            // events "generated" by script/log
            ScriptDamage,
            ScriptKill,
            Damage,
            Kill,
            JoinTeam,
        }

        public GameEvent(EventType t, string d, Player O, Player T, Server S)
        {
            Type = t;
            Data = d?.Trim();
            Origin = O;
            Target = T;
            Owner = S;
            OnProcessed = new ManualResetEventSlim();
            Time = DateTime.UtcNow;
            CurrentEventId++;
            Id = CurrentEventId;
        }

        public GameEvent()
        {
            OnProcessed = new ManualResetEventSlim();
            Time = DateTime.UtcNow;
            CurrentEventId++;
            Id = CurrentEventId;
        }

        private static long CurrentEventId;

        public EventType Type;
        public string Data; // Data is usually the message sent by player
        public string Message;
        public Player Origin;
        public Player Target;
        public Server Owner;
        public Boolean Remote = false;
        public object Extra { get; set; }
        public ManualResetEventSlim OnProcessed { get; set; }
        public DateTime Time { get; private set; }
        public long Id { get; private set; }

        /// <summary>
        /// determine whether an event should be delayed or not
        /// applies only to the origin entity
        /// </summary>
        /// <param name="queuedEvent">event to determine status for</param>
        /// <returns>true if event should be delayed, false otherwise</returns>
        public static bool ShouldOriginEventBeDelayed(GameEvent queuedEvent)
        {
            return queuedEvent.Origin != null &&
                                    !queuedEvent.Origin.IsAuthenticated &&
                                    queuedEvent.Origin.State != Player.ClientState.Connected &&
                                    // we want to allow join and quit events
                                    queuedEvent.Type != EventType.Join &&
                                    queuedEvent.Type != EventType.Quit &&
                                    // we don't care about unknown events
                                    queuedEvent.Origin.NetworkId != 0;
        }

        /// <summary>
        /// determine whether an event should be delayed or not
        /// applies only to the target entity
        /// </summary>
        /// <param name="queuedEvent">event to determine status for</param>
        /// <returns>true if event should be delayed, false otherwise</returns>
        public static bool ShouldTargetEventBeDelayed(GameEvent queuedEvent)
        {
            return queuedEvent.Target != null &&
                                    !queuedEvent.Target.IsAuthenticated &&
                                    queuedEvent.Target.State != Player.ClientState.Connected &&
                                    queuedEvent.Target.NetworkId != 0;
        }
    }
}
