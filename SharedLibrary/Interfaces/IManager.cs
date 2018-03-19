using System.Collections.Generic;
using SharedLibrary.Objects;
using SharedLibrary.Database.Models;
using SharedLibrary.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SharedLibrary.Configuration;

namespace SharedLibrary.Interfaces
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
    }
}
