using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Services;

namespace SharedLibraryCore.Interfaces
{
    public interface IManager
    {
        IReadOnlyList<IManagerCommand> Commands { get; }

        /// <summary>
        ///     enumerates the registered plugin instances
        /// </summary>
        IEnumerable<IPlugin> Plugins { get; }

        IList<IRConParser> AdditionalRConParsers { get; }
        IList<IEventParser> AdditionalEventParsers { get; }
        IMiddlewareActionHandler MiddlewareActionHandler { get; }
        IList<Func<GameEvent, bool>> CommandInterceptors { get; }
        string Version { get; }
        ITokenAuthentication TokenAuthenticator { get; }
        string ExternalIPAddress { get; }
        CancellationToken CancellationToken { get; }
        bool IsRestartRequested { get; }
        bool IsRunning { get; }
        ConcurrentDictionary<long, GameEvent> ProcessingEvents { get; }
        Task Init();
        Task Start();
        Task Stop();
        void Restart();

        [Obsolete]
        ILogger GetLogger(long serverId);

        IList<Server> GetServers();
        IList<IManagerCommand> GetCommands();
        IList<MessageToken> GetMessageTokens();
        IList<EFClient> GetActiveClients();
        EFClient FindActiveClient(EFClient client);
        IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings();
        ClientService GetClientService();
        PenaltyService GetPenaltyService();

        /// <summary>
        ///     provides a page list to add and remove from
        /// </summary>
        /// <returns></returns>
        IPageList GetPageList();

        /// <summary>
        ///     provides a method to execute database operations by name without exposing the
        ///     service level methods
        ///     todo: this could be made obsolete by creating a seperate service library with more concrete definitions
        /// </summary>
        /// <param name="operationName"></param>
        /// <returns></returns>
        Task<IList<T>> ExecuteSharedDatabaseOperation<T>(string operationName);

        void RegisterSharedDatabaseOperation(Task<IList> operation, string operationName);

        /// <summary>
        ///     generates an rcon parser that can be configured by script plugins
        /// </summary>
        /// <param name="name">name of the RCon parser</param>
        /// <returns>new rcon parser instance</returns>
        IRConParser GenerateDynamicRConParser(string name);

        /// <summary>
        ///     Generates an event parser that can be configured by script plugins
        /// </summary>
        /// <param name="name">name of the event parser</param>
        /// <returns>new event parser instance</returns>
        IEventParser GenerateDynamicEventParser(string name);

        Task ExecuteEvent(GameEvent gameEvent);

        /// <summary>
        ///     queues an event for processing
        /// </summary>
        /// <param name="gameEvent">event to be processed</param>
        void AddEvent(GameEvent gameEvent);

        /// <summary>
        ///     adds an additional (script) command to the command list
        /// </summary>
        /// <param name="command"></param>
        void AddAdditionalCommand(IManagerCommand command);

        /// <summary>
        ///     removes a command by its name
        /// </summary>
        /// <param name="name">name of command</param>
        void RemoveCommandByName(string name);

        /// <summary>
        ///     event executed when event has finished executing
        /// </summary>
        event EventHandler<GameEvent> OnGameEventExecuted;
        
        IAlertManager AlertManager { get; }
    }
}
