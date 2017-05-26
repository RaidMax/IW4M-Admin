using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public class Penalty
    {
        public Penalty(Type BType, String Reas, String TargID, String From, DateTime time, String ip)
        {
            Reason = Reas.Replace("!","");
            npID = TargID;
            bannedByID = From;
            When = time;
            IP = ip;
            this.BType = BType;
        }

        public String getWhen()
        {
            return When.ToString("MM/dd/yy HH:mm:ss"); ;
        }

        public enum Type
        {
            Warning,
            Kick,
            TempBan,
            Ban
        }

        public String Reason { get; private set; }
        public String npID { get; private set; }
        public String bannedByID { get; private set; }
        public DateTime When { get; private set; }
        public String IP { get; private set; }
        public Type BType { get; private set; }
    }
}
