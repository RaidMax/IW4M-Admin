using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Ban
    { 
        public Ban(String Reas, String TargID, String From, DateTime time, String ip)
        {
            Reason = Reas;
            npID = TargID;
            bannedByID = From;
            When = time;
            IP = ip;
        }

        public String getReason()
        {
            return Reason;
        }

        public String getID()
        {
            return npID;
        }

        public String getBanner()
        {
            return bannedByID;
        }

        public String getIP()
        {
            return IP;
        }

        public String getWhen()
        {
            return When.ToString("MM/dd/yy HH:mm:ss"); ;
        }

        public DateTime getTime()
        {
            return When;
        }
       
        private String Reason;
        private String npID;
        private String bannedByID;
        private DateTime When;
        private String IP;

    }
 
}
