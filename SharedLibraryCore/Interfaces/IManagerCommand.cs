using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// Defines the basic properties of a command
    /// </summary>
    public interface IManagerCommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <param name="gameEvent">event corresponding to the command</param>
        /// <returns></returns>
        Task ExecuteAsync(GameEvent gameEvent);

        /// <summary>
        /// Name of the command
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the command
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Alternative name of the command
        /// </summary>
        string Alias { get; }

        /// <summary>
        /// Minimum permission required to execute the command
        /// </summary>
        Permission Permission { get; }

        /// <summary>
        /// Syntax for using the command
        /// </summary>
        string Syntax { get; }

        /// <summary>
        /// Indicates if target is required
        /// </summary>
        bool RequiresTarget { get; }
    }
}
