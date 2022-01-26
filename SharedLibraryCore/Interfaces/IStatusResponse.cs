using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     describes the collection of data returned from a status query
    /// </summary>
    public interface IStatusResponse
    {
        /// <summary>
        ///     name of the map
        /// </summary>
        string Map { get; }

        /// <summary>
        ///     gametype/mode
        /// </summary>
        string GameType { get; }

        /// <summary>
        ///     server name
        /// </summary>
        string Hostname { get; }

        /// <summary>
        ///     max number of players
        /// </summary>
        int? MaxClients { get; }

        /// <summary>
        ///     active clients
        /// </summary>
        EFClient[] Clients { get; }
    }
}