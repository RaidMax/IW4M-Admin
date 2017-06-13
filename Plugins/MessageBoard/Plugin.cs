using System;
using SharedLibrary;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;

namespace MessageBoard.Plugin
{
    public class Main : IPlugin
    {
        public static Forum.Manager ManagerInstance { get; private set; }

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
            ManagerInstance = new Forum.Manager();
            ManagerInstance.Start();
        }

        public async Task OnUnloadAsync()
        {
            ManagerInstance.Stop();
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
        }
    }
}
