using System;
using SharedLibrary;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;

namespace MessageBoard.Plugin
{
    public class Main : IPlugin
    {
        public static Forum.Manager forum { get; private set; }
        public static Server stupidServer { get; private set; }

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
                return 0.1f;
            }
        }

        public string Name
        {
            get
            {
                return "Message Board Plugin";
            }
        }

        public async Task OnLoadAsync()
        {
            forum = new Forum.Manager();
            forum.Start();
        }

        public async Task OnUnloadAsync()
        {
            forum.Stop();
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                if (stupidServer == null)
                    stupidServer = S;
            }
        }
    }
}
