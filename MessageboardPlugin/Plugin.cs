using System;
using SharedLibrary;
using SharedLibrary.Extensions;
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

        public async Task OnLoad()
        {
            await Task.Run(() =>
            {
               forum = new Forum.Manager();
               forum.Start();
            });
        }

        public async Task OnUnload()
        {
            forum.Stop();
        }

        public async Task OnTick(Server S)
        {
            return;
        }

        public async Task OnEvent(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                if (stupidServer == null)
                    stupidServer = S;
            }
        }
    }
}
