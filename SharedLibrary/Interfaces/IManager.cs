using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
