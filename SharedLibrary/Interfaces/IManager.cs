using System.Collections.Generic;

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
        IPenaltyList GetClientPenalties();
        ClientsDB GetClientDatabase();
        AliasesDB GetAliasesDatabase();
        IList<Helpers.MessageToken> GetMessageTokens();
        IList<Player> GetActiveClients();
        IList<Player> GetAliasClients(Player player);
        IList<Aliases> GetAliases(Player player);
        IList<Player> GetPrivilegedClients();
    }
}
