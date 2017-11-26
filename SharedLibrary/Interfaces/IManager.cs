using System.Collections.Generic;
using SharedLibrary.Objects;
using SharedLibrary.Database.Models;
using SharedLibrary.Services;

namespace SharedLibrary.Interfaces
{
    public interface IManager
    {
        void Init();
        void Start();
        void Stop();
        ILogger GetLogger();
        IList<Server> GetServers();
        IList<Command> GetCommands();
        IList<Helpers.MessageToken> GetMessageTokens();
        IList<Player> GetActiveClients();
       ClientService GetClientService();
        AliasService GetAliasService();
        PenaltyService GetPenaltyService();
    }
}
