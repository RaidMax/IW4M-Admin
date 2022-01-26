using System.Threading.Tasks;
using static Data.Models.Client.EFClient;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     Defines the basic properties of a command
    /// </summary>
    public interface IManagerCommand
    {
        /// <summary>
        ///     Name of the command
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Description of the command
        /// </summary>
        string Description { get; }

        /// <summary>
        ///     Alternative name of the command
        /// </summary>
        string Alias { get; }

        /// <summary>
        ///     Minimum permission required to execute the command
        /// </summary>
        Permission Permission { get; }

        /// <summary>
        ///     Games the command is supported on
        /// </summary>
        Game[] SupportedGames { get; }

        /// <summary>
        ///     Syntax for using the command
        /// </summary>
        string Syntax { get; }

        /// <summary>
        ///     Indicates if target is required
        /// </summary>
        bool RequiresTarget { get; }

        /// <summary>
        ///     Indicates if the commands can be run as another client
        /// </summary>
        bool AllowImpersonation { get; }

        /// <summary>
        ///     Indicates if the command result should be broadcasted to all clients
        /// </summary>
        bool IsBroadcast { get; set; }

        /// <summary>
        ///     Executes the command
        /// </summary>
        /// <param name="gameEvent">event corresponding to the command</param>
        /// <returns></returns>
        Task ExecuteAsync(GameEvent gameEvent);
    }
}