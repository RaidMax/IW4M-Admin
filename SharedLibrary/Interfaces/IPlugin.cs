using System;
using System.Threading.Tasks;

namespace SharedLibrary.Interfaces
{
    public interface IPlugin
    {
        Task OnLoad();
        Task OnUnload();
        Task OnEvent(Event E, Server S);
        Task OnTick(Server S);

        //for logging purposes
        String Name { get; }
        float Version { get; }  
        String Author { get; }
    }
}
