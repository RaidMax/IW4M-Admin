using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IConfigurationHandler<T> where T : IBaseConfiguration
    {
        string FileName { get; }
        Task Save();
        Task BuildAsync();
        T Configuration();
        void Set(T config);
    }
}
