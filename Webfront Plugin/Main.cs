using System;
using SharedLibrary;
using System.Threading;

namespace Webfront_Plugin
{
    public class Webfront : Notify
    {
        private static Manager webManager;

        public override void onEvent(Event E)
        {
            if (webManager != null)
            {
                if (E.Type == Event.GType.Start)
                {
                    Manager.webFront.addServer(E.Owner);
                    E.Owner.Log.Write("Webfront now has access to server on port " + E.Owner.getPort(), Log.Level.Production);
                }
                if (E.Type == Event.GType.Stop)
                {
                    Manager.webFront.removeServer(E.Owner);
                    E.Owner.Log.Write("Webfront has lost access to server on port " + E.Owner.getPort(), Log.Level.Production);
                }
            }
        }

        public override void onLoad()
        {
            webManager = new Manager();
            Thread webManagerThread = new Thread(new ThreadStart(webManager.Init));
            webManagerThread.Name = "Webfront";

            webManagerThread.Start();
        }
    }
}
