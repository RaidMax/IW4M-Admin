using System.Collections.Generic;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using System.Threading.Tasks;
using SharedLibraryCore.Configuration;

namespace SharedLibraryCore.Interfaces
{
    public interface IManager
    {
        Task Init();
        void Start();
        void Stop();
        ILogger GetLogger();
        IList<Server> GetServers();
        IList<Command> GetCommands();
        IList<Helpers.MessageToken> GetMessageTokens();
        IList<Player> GetActiveClients();
        IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings();
        ClientService GetClientService();
        AliasService GetAliasService();
        PenaltyService GetPenaltyService();
        IDictionary<int, Player> GetPrivilegedClients();
    }
}
