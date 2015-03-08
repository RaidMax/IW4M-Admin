using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Ban
    { 
        public Ban(String Reas, String TargID, String From)
        {
            Reason = Reas;
            npID = TargID;
            bannedByID = From;
            When = DateTime.Now;
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
       
        private String Reason;
        private String npID;
        private String bannedByID;
        private DateTime When;

    }
 
}
