using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SharedLibrary
{
    [Serializable]
    public class Chat
    {
        public Chat(string O, String M, DateTime D)
        {
            Name = O;
            Message = M;
            Time = D;
            
        }

        public String timeString()
        {
            return Time.ToShortTimeString();
        }

        //public Player Origin { get; private set; }
        public String Message { get; private set; }
        public DateTime Time { get; private set; }
        public string Name;
    }

    [Serializable]
    public struct RestEvent
    {
        public RestEvent(eType Ty, eVersion V, string M, string T, string O, string Ta)
        {
            Type = Ty;
            Version = V;
            Message = M;
            Title = T;
            Origin = O;
            Target = Ta;

            ID = Math.Abs(DateTime.Now.GetHashCode());
        }

        public enum eType
        {
            NOTIFICATION,
            STATUS,
            ALERT,
        }

        public enum eVersion
        {
            IW4MAdmin
        }

        public eType Type;
        public eVersion Version;
        public string Message;
        public string Title;
        public string Origin;
        public string Target;
        public int ID;
    }


    public class Event
    {
        public enum GType
        {
            //FROM SERVER
            Start,
            Stop,
            Connect,
            Disconnect,
            Say,
            Kill,
            Death,
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
            Report
        }

        public Event(GType t, string d, Player O, Player T, Server S)
        {
            Type = t;
            Data = d;
            Origin = O;
            Target = T;
            Owner = S;
        }

        public static Event ParseEventString(String[] line, Server SV)
        {
#if DEBUG == false
            try
#endif
            {
                String eventType = line[0].Substring(line[0].Length - 1);
                eventType = eventType.Trim();

                if (eventType == "K")
                {
                    StringBuilder Data = new StringBuilder();
                    if (line.Length > 9)
                    {
                        for (int i = 9; i < line.Length; i++)
                            Data.Append(line[i] + ";");
                    }

                    return new Event(GType.Kill, Data.ToString(), SV.ParseClientFromString(line, 6), SV.ParseClientFromString(line, 2), SV);
                }

                if (line[0].Substring(line[0].Length - 3).Trim() == "say")
                {
                    Regex rgx = new Regex("[^a-zA-Z0-9 -! -_]");
                    string message = rgx.Replace(line[4], "");
                    return new Event(GType.Say, Utilities.removeNastyChars(message).StripColors(), SV.ParseClientFromString(line, 2), null, SV) { Message = Utilities.removeNastyChars(message).StripColors() };
                }

                if (eventType == ":")
                    return new Event(GType.MapEnd, line[0], new Player("WORLD", "WORLD", 0, 0), null, SV);

                if (line[0].Contains("InitGame"))
                    return new Event(GType.MapChange, line[0], new Player("WORLD", "WORLD", 0, 0), null, SV);


                return null;
            }
#if DEBUG == false
            catch (Exception E)
            {
                SV.Manager.GetLogger().WriteError("Error requesting event " + E.Message);
                return null;
            }
#endif
        }


        public GType Type;
        public string Data; // Data is usually the message sent by player
        public string Message;
        public Player Origin;
        public Player Target;
        public Server Owner;
        public Boolean Remote = false;
    }
}
