using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IConfigurationHandler<T> where T : IBaseConfiguration
    {
        string FileName { get; }
        Task Save();
        void Build();
        T Configuration();
        void Set(T config);
    }
}