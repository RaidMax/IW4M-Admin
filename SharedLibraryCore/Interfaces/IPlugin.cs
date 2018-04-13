using System;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IPlugin
    {
        Task OnLoadAsync(IManager manager);
        Task OnUnloadAsync();
        Task OnEventAsync(GameEvent E, Server S);
        Task OnTickAsync(Server S);

        //for logging purposes
        String Name { get; }
        float Version { get; }  
        String Author { get; }
    }
}
