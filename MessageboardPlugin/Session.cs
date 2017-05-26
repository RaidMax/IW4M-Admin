using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageBoard
{
    public class Session
    {
        public User sessionUser;
        public string sessionID { get; private set; }
        public DateTime sessionStartTime;

        public Session(User sessionUser, string sessionID)
        {
            this.sessionUser    = sessionUser;
            this.sessionID      = sessionID;
            sessionStartTime    = DateTime.Now;
        }

    }
}
