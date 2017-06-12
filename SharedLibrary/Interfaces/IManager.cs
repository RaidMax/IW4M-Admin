using System.Collections.Generic;

namespace SharedLibrary.Interfaces
{
    public interface IManager
    {
        void Init();
        void Start();
        void Stop();
        ILogger GetLogger();
        List<Server> GetServers();
        List<Command> GetCommands();
        IPenaltyList GetClientPenalties();
        ClientsDB GetClientDatabase();
        AliasesDB GetAliasesDatabase();
        IList<MessageToken> GetMessageTokens();
        IList<Player> GetActiveClients();
    }
}
