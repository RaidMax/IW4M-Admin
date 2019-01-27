using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Objects;
using SharedLibraryCore.RCon;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParser
    {
        Task<Dvar<T>> GetDvarAsync<T>(Connection connection, string dvarName);
        Task<bool> SetDvarAsync(Connection connection, string dvarName, object dvarValue);
        Task<string[]> ExecuteCommandAsync(Connection connection, string command);
        Task<List<EFClient>> GetStatusAsync(Connection connection);
        IRConParserConfiguration Configuration { get; set; }
    }
}
