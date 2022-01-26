using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IPlugin
    {
        string Name { get; }
        float Version { get; }
        string Author { get; }
        bool IsParser => false;
        Task OnLoadAsync(IManager manager);
        Task OnUnloadAsync();
        Task OnEventAsync(GameEvent E, Server S);
        Task OnTickAsync(Server S);
    }
}