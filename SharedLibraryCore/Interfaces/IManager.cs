using System.Collections.Generic;
using System.Threading.Tasks;

using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using SharedLibraryCore.Configuration;
using System.Reflection;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces
{
    public interface IManager
    {
        Task Init();
        void Start();
        void Stop();
        ILogger GetLogger(long serverId);
        IList<Server> GetServers();
        IList<Command> GetCommands();
        IList<Helpers.MessageToken> GetMessageTokens();
        IList<EFClient> GetActiveClients();
        IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings();
        ClientService GetClientService();
        AliasService GetAliasService();
        PenaltyService GetPenaltyService();
        IDictionary<int, EFClient> GetPrivilegedClients();
        /// <summary>
        /// Get the event handlers
        /// </summary>
        /// <returns>EventHandler for the manager</returns>
        IEventHandler GetEventHandler();
        /// <summary>
        /// Signal to the manager that event(s) needs to be processed
        /// </summary>
        void SetHasEvent();
        bool ShutdownRequested();
        IList<Assembly> GetPluginAssemblies();
        /// <summary>
        /// provides a page list to add and remove from
        /// </summary>
        /// <returns></returns>
        IPageList GetPageList();
        IList<IRConParser> AdditionalRConParsers { get; }
        IList<IEventParser> AdditionalEventParsers { get; }
        IRConParser GenerateDynamicRConParser();
        IEventParser GenerateDynamicEventParser();
        string Version { get;}
        ITokenAuthentication TokenAuthenticator { get; }
        string ExternalIPAddress { get; }
    }
}
