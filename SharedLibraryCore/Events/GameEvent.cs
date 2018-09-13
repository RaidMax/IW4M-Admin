using System;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Objects;

namespace SharedLibraryCore
{
    public class GameEvent
    {
        public enum EventType
        {
            /// <summary>
            /// the event wasn't parsed properly
            /// </summary>
            Unknown,

            // events "generated" by the server
            /// <summary>
            /// a server started being monitored
            /// </summary>
            Start,
            /// <summary>
            /// a server stopped being monitored
            /// </summary>
            Stop,
            /// <summary>
            /// a client was detecting as connecting via RCon
            /// </summary>
            Connect,
            /// <summary>
            /// a client was detecting joining via log
            /// </summary>
            Join,
            /// <summary>
            /// a client was detected leaving via log
            /// </summary>
            Quit,
            /// <summary>
            /// a client was detected leaving by RCon
            /// </summary>
            Disconnect,
            /// <summary>
            /// the current map ended
            /// </summary>
            MapEnd,
            /// <summary>
            /// the current map changed
            /// </summary>
            MapChange,

            // events "generated" by clients  
            /// <summary>
            /// a client sent a message
            /// </summary>
            Say,
            /// <summary>
            /// a client was warned
            /// </summary>
            Warn,
            /// <summary>
            /// a client was reported
            /// </summary>
            Report,
            /// <summary>
            /// a client was flagged
            /// </summary>
            Flag,
            /// <summary>
            /// a client was unflagged
            /// </summary>
            Unflag,
            /// <summary>
            /// a client was kicked
            /// </summary>
            Kick,
            /// <summary>
            /// a client was tempbanned
            /// </summary>
            TempBan,
            /// <summary>
            /// a client was banned
            /// </summary>
            Ban,
            /// <summary>
            /// a client entered a command
            /// </summary>
            Command,
            /// <summary>
            /// a client's permission was changed
            /// </summary>
            ChangePermission,

            // events "generated" by IW4MAdmin
            /// <summary>
            /// a message is sent to all clients
            /// </summary>
            Broadcast,
            /// <summary>
            /// a message is sent to a specific client
            /// </summary>
            Tell,

            // events "generated" by script/log
            /// <summary>
            /// AC Damage Log
            /// </summary>
            ScriptDamage,
            /// <summary>
            /// AC Kill Log
            /// </summary>
            ScriptKill,
            /// <summary>
            /// damage info printed out by game script
            /// </summary>
            Damage,
            /// <summary>
            /// kill info printed out by game script
            /// </summary>
            Kill,
            /// <summary>
            /// team info printed out by game script
            /// </summary>
            JoinTeam,
        }

        static long NextEventId;
        static long GetNextEventId() => Interlocked.Increment(ref NextEventId);

        public GameEvent()
        {
            OnProcessed = new ManualResetEventSlim(false);
            Time = DateTime.UtcNow;
            Id = GetNextEventId();
        }

        public EventType Type;
        public string Data; // Data is usually the message sent by player
        public string Message;
        public Player Origin;
        public Player Target;
        public Server Owner;
        public Boolean Remote = false;
        public object Extra { get; set; }
        public ManualResetEventSlim OnProcessed { get; set; }
        public DateTime Time { get; set; }
        public long Id { get; private set; }

        /// <summary>
        /// asynchronously wait for GameEvent to be processed
        /// </summary>
        /// <returns>waitable task </returns>
        public Task<bool> WaitAsync(int timeOut = int.MaxValue) => Task.FromResult(OnProcessed.Wait(timeOut));

        /// <summary>
        /// determine whether an event should be delayed or not
        /// applies only to the origin entity
        /// </summary>
        /// <param name="queuedEvent">event to determine status for</param>
        /// <returns>true if event should be delayed, false otherwise</returns>
        public static bool ShouldOriginEventBeDelayed(GameEvent queuedEvent)
        {
            return queuedEvent.Origin != null &&
                                    queuedEvent.Origin.State != Player.ClientState.Connected &&
                                    // we want to allow join and quit events
                                    queuedEvent.Type != EventType.Connect &&
                                    queuedEvent.Type != EventType.Join &&
                                    queuedEvent.Type != EventType.Quit &&
                                    queuedEvent.Type != EventType.Disconnect &&
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
                                    queuedEvent.Target.State != Player.ClientState.Connected &&
                                    queuedEvent.Target.NetworkId != 0;
        }

        public static bool IsEventTimeSensitive(GameEvent gameEvent) => gameEvent.Type == EventType.Connect;
    }
}
