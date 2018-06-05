using System;
using System.Threading;
using SharedLibraryCore.Objects;

namespace SharedLibraryCore
{
    public class GameEvent
    {
        public enum EventType
        {
            //FROM SERVER
            Start,
            Stop,
            Connect,
            // this is for IW5 compatibility
            Join,
            Disconnect,
            Say,
            MapChange,
            MapEnd,

            //FROM ADMIN
            Broadcast,
            Tell,
            Kick,
            Ban,
            Remote,
            Unknown,

            //FROM PLAYER
            Report,
            Flag,
            Command,

            // FROM GAME
            ScriptDamage,
            ScriptKill,
            Kill,
            Damage,
            Death,
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
    }
}
