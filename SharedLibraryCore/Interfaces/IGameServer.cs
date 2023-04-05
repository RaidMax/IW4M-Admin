using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Models;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces
{
    public interface IGameServer
    {
        /// <summary>
        ///     kicks target on behalf of origin for given reason
        /// </summary>
        /// <param name="reason">reason client is being kicked</param>
        /// <param name="target">client to kick</param>
        /// <param name="origin">source of kick action</param>
        /// <param name="previousPenalty">previous penalty the kick is occuring for (if applicable)</param>
        /// <returns></returns>
        Task Kick(string reason, EFClient target, EFClient origin, EFPenalty previousPenalty = null);
        
        /// <summary>
        /// Time the most recent match ended
        /// </summary>
        DateTime? MatchEndTime { get; }
        
        /// <summary>
        /// Time the current match started
        /// </summary>
        DateTime? MatchStartTime { get; }
        
        /// <summary>
        /// List of connected clients
        /// </summary>
        IReadOnlyList<EFClient> ConnectedClients { get; }
        
        /// <summary>
        /// Game code corresponding to the development studio project
        /// </summary>
        Reference.Game GameCode { get; }
        
        /// <summary>
        /// Indicates if the anticheat/custom callbacks/live radar integration is enabled
        /// </summary>
        bool IsLegacyGameIntegrationEnabled { get; }
        
        /// <summary>
        /// Unique identifier for the server (typically ip:port)
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Network address the server is listening on
        /// </summary>
        string ListenAddress { get; }
        
        /// <summary>
        /// Network port the server is listening on
        /// </summary>
        int ListenPort { get; }
        
        /// <summary>
        /// Name of the server (hostname)
        /// </summary>
        string ServerName { get; }
        
        /// <summary>
        /// Current gametype
        /// </summary>
        string Gametype { get; }
        
        /// <summary>
        /// Game password (required to join)
        /// </summary>
        string GamePassword { get; }
        
        /// <summary>
        /// Current map the game server is running
        /// </summary>
        Map Map { get; }
        
        /// <summary>
        /// Database id for EFServer table and references
        /// </summary>
        long LegacyDatabaseId { get; }
    }
}
