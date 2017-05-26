using System;
using System.Collections.Generic;
using SharedLibrary;


namespace ChatMonitor
{
    public class Main : IPlugin
    {


        public string Author
        {
            get
            {
                return "RaidMax";
            }
        }

        public float Version
        {
            get
            {
                return 1.0f;
            }
        }

        public string Name
        {
            get
            {
                return "Chat Monitor Plugin";
            }
        }


        public void onLoad()
        {
            lastClear = DateTime.Now;
            flaggedMessages = 0;
            flaggedMessagesText = new List<string>();
        }

        public void onUnload()
        {
            return;
        }

        public void onTick(Server S)
        {
            return;
        }

        public void onEvent(Event E, Server S)
        {
            
        }
    }
}
