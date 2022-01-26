using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Interfaces
{
    public interface IModelConfiguration
    {
        void Configure(ModelBuilder builder);
    }
}