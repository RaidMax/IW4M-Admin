using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParser
    {
        Task<Dvar<T>> GetDvarAsync<T>(RCon.Connection connection, string dvarName);
        Task<bool> SetDvarAsync(RCon.Connection connection, string dvarName, object dvarValue);
        Task<string[]> ExecuteCommandAsync(RCon.Connection connection, string command);
        Task<List<Player>> GetStatusAsync(RCon.Connection connection);
    }
}
