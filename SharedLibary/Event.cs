using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SharedLibrary
{
    public class Chat
    {
        public Chat(Player O, String M, DateTime D)
        {
            Origin = O;
            Message = M;
            Time = D;
        }

        public String timeString()
        {
            return Time.ToShortTimeString();
        }

        public Player Origin { get; private set; }
        public String Message { get; private set; }
        public DateTime Time { get; private set; }
    }

    public class Event
    {
        public enum GType
        {
            //FROM SERVER
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
            Unknown,
        }

        public Event(GType t, string d, Player O, Player T, Server S)
        {
            Type = t;
            Data = d;
            Origin = O;
            Target = T;
            Owner = S;
        }

        public Command isValidCMD(List<Command> list)
        {
            if (this.Data.Substring(0, 1) == "!")
            {
                string[] cmd = this.Data.Substring(1, this.Data.Length - 1).Split(' ');

                foreach (Command C in list)
                {
                    if (C.Name == cmd[0].ToLower() || C.Alias == cmd[0].ToLower())
                        return C;
                }

                return null;
            }

            else
                return null;
        }

        public static Event requestEvent(String[] line, Server SV)
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

                    return new Event(GType.Kill, Data.ToString(), SV.clientFromEventLine(line, 6), SV.clientFromEventLine(line, 2), SV);
                }

                if (line[0].Substring(line[0].Length - 3).Trim() == "say")
                {
                    Regex rgx = new Regex("[^a-zA-Z0-9 -! -_]");
                    string message = rgx.Replace(line[4], "");
                    return new Event(GType.Say, Utilities.removeNastyChars(message), SV.clientFromEventLine(line, 2), null, SV);
                }

                if (eventType == ":")
                    return new Event(GType.MapEnd, line[0], new Player("WORLD", "WORLD", 0, 0), null, SV);

                if (line[0].Split('\\').Length > 5) // blaze it
                    return new Event(GType.MapChange, line[0], new Player("WORLD", "WORLD", 0, 0), null, SV);


                return null;
            }
#if DEBUG == false
            catch (Exception E)
            {
                SV.Log.Write("Error requesting event " + E.Message, Log.Level.Debug);
                return null;
            }
#endif
        }


        public GType Type;
        public string Data; // Data is usually the message sent by player
        public Player Origin;
        public Player Target;
        public Server Owner;
    }

    public abstract class EventNotify
    {
        public abstract void onEvent(Event E);
    }
}
