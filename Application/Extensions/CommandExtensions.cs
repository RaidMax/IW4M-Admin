using IW4MAdmin.Application.Misc;
using SharedLibraryCore.Interfaces;
using System.Linq;

namespace IW4MAdmin.Application.Extensions
{
    public static class CommandExtensions
    {
        /// <summary>
        /// determines the command configuration name for given manager command
        /// </summary>
        /// <param name="command">command to determine config name for</param>
        /// <returns></returns>
        public static string CommandConfigNameForType(this IManagerCommand command)
        {
            return command.GetType() == typeof(ScriptCommand) ?
                        $"{char.ToUpper(command.Name[0])}{command.Name.Substring(1)}Command" :
                        command.GetType().Name;
        }
    }
}
