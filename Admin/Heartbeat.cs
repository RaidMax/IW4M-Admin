using System;
using System.Collections.Generic;
using System.Text;
using SharedLibrary;

namespace IW4MAdmin
{
    class Heartbeat
    {
        public Heartbeat(Server I)
        {
            Handle = new Connection("http://raidmax.org/IW4M/Admin");
        }

        public void Send(Server S)
        {
            String URI = String.Format("http://raidmax.org/IW4M/Admin/heartbeat.php?port={0}&name={1}&map={2}&players={3}&version={4}&gametype={5}&servercount={6}", S.getPort(), S.getName(), S.CurrentMap.Name, S.getPlayers().Count, IW4MAdmin.Program.Version.ToString(), S.Gametype, Manager.GetInstance().Servers);
            // blind fire
            Handle.Request(URI);
        }

        private Connection Handle;
    }
}
