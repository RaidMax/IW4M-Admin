using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IConfigurationHandler<T> where T : IBaseConfiguration
    {
        Task Save();
        void Build();
        T Configuration();
        void Set(T config);
        string FileName { get; }
    }
}
