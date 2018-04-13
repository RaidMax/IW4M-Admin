using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

            // FROM GAME
            Script,
            Kill,
            Death,
        }

        public GameEvent(EventType t, string d, Player O, Player T, Server S)
        {
            Type = t;
            Data = d?.Trim();
            Origin = O;
            Target = T;
            Owner = S;
        }

        public GameEvent() { }


        public EventType Type;
        public string Data; // Data is usually the message sent by player
        public string Message;
        public Player Origin;
        public Player Target;
        public Server Owner;
        public Boolean Remote = false;
    }
}
