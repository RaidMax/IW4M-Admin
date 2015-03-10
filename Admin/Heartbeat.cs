using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class Heartbeat
    {
        public Heartbeat(Server I)
        {
            Handle = new Connection("http://raidmax.org/IW4M/Admin");
            Instance = I;
        }

        public void Send()
        {
            String URI = String.Format("http://raidmax.org/IW4M/Admin/heartbeat.php?address={0}&name={1}&map={2}&players={3}", Instance.getPort().ToString(), Instance.getName(), Instance.getMap(), Instance.getClientNum().ToString());
            Handle.Request(URI);
        }

        private Connection Handle;
        private Server Instance;
    }
}
