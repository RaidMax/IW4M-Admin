using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Admin
    {
        public Admin()
        {
            Time = DateTime.Now;
            Server = new Server("127.0.0.1", 28960, "NO");
        }

        public Server Server;

        public static String getTime()
        {
            return DateTime.Now.ToString("H:mm:ss");
        }

        public void Monitor()
        {
            Server.Monitor();
        }

        private DateTime Time;
    }
}
