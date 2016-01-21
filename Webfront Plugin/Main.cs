using System;
using SharedLibrary;
using System.Threading;
using System.Collections.Generic;

namespace Webfront_Plugin
{
    public class Webfront : Plugin
    {
        private static Thread webManagerThread;

        public override void onEvent(Event E)
        {
            if (E.Type == Event.GType.Start)
            {
                Manager.webFront.removeServer(Manager.webFront.getServers().Find(x => x.getPort() == E.Owner.getPort()));
                Manager.webFront.addServer(E.Owner);
                E.Owner.Log.Write("Webfront now listening", Log.Level.Production);
            }
            if (E.Type == Event.GType.Stop)
            {
                Manager.webFront.removeServer(E.Owner);
                E.Owner.Log.Write("Webfront has lost access to server", Log.Level.Production);
            }
        }

        public override void onLoad()
        {
            webManagerThread = new Thread(new ThreadStart(Manager.Init));
            webManagerThread.Name = "Webfront";

            webManagerThread.Start();
        }

        public override void onUnload()
        {
            Manager.webScheduler.Stop();
            webManagerThread.Join();
        }

        public override String Name
        {
            get { return "Webfront"; }
        }

        public override float Version
        {
            get { return 0.1f; }
        }

        public override string Author
        {
            get
            {
                return "RaidMax";
            }
        }
    }
}
