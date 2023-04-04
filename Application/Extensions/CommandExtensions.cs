using System;
using System.Collections.Generic;
using SharedLibraryCore.Interfaces;
using System.Linq;
using IW4MAdmin.Application.Plugin.Script;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;

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
            return command.GetType() == typeof(ScriptCommand)
                ? $"{char.ToUpper(command.Name[0])}{command.Name.Substring(1)}Command"
                : command.GetType().Name;
        }

        public static IList<Map> FindMap(this Server server, string mapName) => server.Maps.Where(map =>
            map.Name.Equals(mapName, StringComparison.InvariantCultureIgnoreCase) ||
            map.Alias.Equals(mapName, StringComparison.InvariantCultureIgnoreCase)).ToList();

        public static IList<Gametype> FindGametype(this DefaultSettings settings, string gameType, Server.Game? game = null) =>
            settings.Gametypes?.Where(gt => game == null || gt.Game == game)
                .SelectMany(gt => gt.Gametypes).Where(gt =>
                    gt.Alias.Contains(gameType, StringComparison.CurrentCultureIgnoreCase) ||
                    gt.Name.Contains(gameType, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }
}
