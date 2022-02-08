using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     defines capabilities of script command factory
    /// </summary>
    public interface IScriptCommandFactory
    {
        /// <summary>
        ///     generate a new script command from parsed source
        /// </summary>
        /// <param name="name">name of command</param>
        /// <param name="alias">alias of command</param>
        /// <param name="description">description of command</param>
        /// <param name="permission">minimum required permission</param>
        /// <param name="isTargetRequired">target required or not</param>
        /// <param name="args">command arguments (name, is required)</param>
        /// <param name="executeAction">action to peform when commmand is executed</param>
        /// <param name="supportedGames"></param>
        /// <returns></returns>
        IManagerCommand CreateScriptCommand(string name, string alias, string description, string permission,
            bool isTargetRequired, IEnumerable<(string, bool)> args, Func<GameEvent, Task> executeAction,
            Server.Game[] supportedGames);
    }
}
