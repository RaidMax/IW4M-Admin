using System;

namespace SharedLibrary
{
    public class Penalty
    {
        public Penalty(Type BType, String Reas, String TargID, String From, DateTime time, String ip)
        {
            Reason = Reas.Replace("!","");
            OffenderID = TargID;
            PenaltyOriginID = From;
            When = time;
            IP = ip;
            this.BType = BType;
        }

        public String GetWhenFormatted()
        {
            return When.ToString("MM/dd/yy HH:mm:ss"); ;
        }

        public enum Type
        {
            Report,
            Warning,
            Flag,
            Kick,
            TempBan,
            Ban
        }

        public String Reason { get; private set; }
        public String OffenderID { get; private set; }
        public String PenaltyOriginID { get; private set; }
        public DateTime When { get; private set; }
        public String IP { get; private set; }
        public Type BType { get; private set; }
    }
}
