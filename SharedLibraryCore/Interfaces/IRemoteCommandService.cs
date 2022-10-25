using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Interfaces;

public interface IRemoteCommandService
{
    Task<IEnumerable<CommandResponseInfo>> Execute(int originId, int? targetId, string command, IEnumerable<string> arguments, Server server);
    Task<(bool, IEnumerable<CommandResponseInfo>)> ExecuteWithResult(int originId, int? targetId, string command, IEnumerable<string> arguments, Server server);
}
