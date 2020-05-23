using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Services;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using System.Threading;
using System.Collections;
using System;

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
        IList<IManagerCommand> GetCommands();
        IList<Helpers.MessageToken> GetMessageTokens();
        IList<EFClient> GetActiveClients();
        IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings();
        ClientService GetClientService();
        AliasService GetAliasService();
        PenaltyService GetPenaltyService();
        /// <summary>
        /// enumerates the registered plugin instances
        /// </summary>
        IEnumerable<IPlugin> Plugins { get; }
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
        bool IsRunning { get; }
        Task ExecuteEvent(GameEvent gameEvent);
        /// <summary>
        /// queues an event for processing
        /// </summary>
        /// <param name="gameEvent">event to be processed</param>
        void AddEvent(GameEvent gameEvent);
        /// <summary>
        /// adds an additional (script) command to the command list
        /// </summary>
        /// <param name="command"></param>
        void AddAdditionalCommand(IManagerCommand command);
        /// <summary>
        /// removes a command by its name
        /// </summary>
        /// <param name="name">name of command</param>
        void RemoveCommandByName(string name);
        /// <summary>
        /// event executed when event has finished executing 
        /// </summary>
        event EventHandler<GameEvent> OnGameEventExecuted;
    }
}
