using System;
using System.Threading.Tasks;

namespace SharedLibrary.Interfaces
{
    public interface IPlugin
    {
        Task OnLoadAsync(Server S);
        Task OnUnloadAsync(Server S);
        Task OnEventAsync(Event E, Server S);
        Task OnTickAsync(Server S);

        //for logging purposes
        String Name { get; }
        float Version { get; }  
        String Author { get; }
    }
}
