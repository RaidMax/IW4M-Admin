using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Services;
using SharedLibraryCore.Configuration;
using System.Reflection;
using SharedLibraryCore.Database.Models;
using System.Threading;
using System.Collections;

namespace SharedLibraryCore.Interfaces
{
    public interface IManager
    {
        Task Init();
        Task Start();
        void Stop();
        void Restart();
        ILogger GetLogger(long serverId);
        IList<Server> GetServers();
        IList<Command> GetCommands();
        IList<Helpers.MessageToken> GetMessageTokens();
        IList<EFClient> GetActiveClients();
        IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings();
        ClientService GetClientService();
        AliasService GetAliasService();
        PenaltyService GetPenaltyService();
        /// <summary>
        /// Get the event handlers
        /// </summary>
        /// <returns>EventHandler for the manager</returns>
        IEventHandler GetEventHandler();
        IList<Assembly> GetPluginAssemblies();
        /// <summary>
        /// provides a page list to add and remove from
        /// </summary>
        /// <returns></returns>
        IPageList GetPageList();
        IList<IRConParser> AdditionalRConParsers { get; }
        IList<IEventParser> AdditionalEventParsers { get; }
        /// <summary>
        /// provides a method to execute database operations by name without exposing the 
        /// service level methods
        /// todo: this could be made obsolete by creating a seperate service library with more concrete definitions
        /// </summary>
        /// <param name="operationName"></param>
        /// <returns></returns>
        Task<IList<T>> ExecuteSharedDatabaseOperation<T>(string operationName);
        void RegisterSharedDatabaseOperation(Task<IList> operation, string operationName);
        IMiddlewareActionHandler MiddlewareActionHandler { get; }

        /// <summary>
        /// generates an rcon parser that can be configured by script plugins
        /// </summary>
        /// <param name="name">name of the RCon parser</param>
        /// <returns>new rcon parser instance</returns>
        IRConParser GenerateDynamicRConParser(string name);

        /// <summary>
        /// Generates an event parser that can be configured by script plugins
        /// </summary>
        /// <param name="name">name of the event parser</param>
        /// <returns>new event parser instance</returns>
        IEventParser GenerateDynamicEventParser(string name);
        string Version { get;}
        ITokenAuthentication TokenAuthenticator { get; }
        string ExternalIPAddress { get; }
        CancellationToken CancellationToken { get; }
        bool IsRestartRequested { get; }
        //OnServerEventEventHandler OnServerEvent { get; set; }
        Task ExecuteEvent(GameEvent gameEvent);
    }
}
