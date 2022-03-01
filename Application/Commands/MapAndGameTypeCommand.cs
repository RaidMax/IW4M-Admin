using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Data.Models.Client;
using IW4MAdmin.Application.Extensions;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Commands
{
    public class MapAndGameTypeCommand : Command
    {
        private const string ArgumentRegexPattern = "(?:\"([^\"]+)\"|([^\\s]+)) (?:\"([^\"]+)\"|([^\\s]+))";
        private readonly ILogger _logger;
        private readonly DefaultSettings _defaultSettings;

        public MapAndGameTypeCommand(ILogger<MapAndGameTypeCommand> logger, CommandConfiguration config,
            DefaultSettings defaultSettings, ITranslationLookup layout) : base(config, layout)
        {
            Name = "mapandgametype";
            Description = _translationLookup["COMMANDS_MAG_DESCRIPTION"];
            Alias = "mag";
            Permission = EFClient.Permission.Administrator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMADS_MAG_ARG_1"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMADS_MAG_ARG_2"],
                    Required = true
                }
            };
            _logger = logger;
            _defaultSettings = defaultSettings;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var match = Regex.Match(gameEvent.Data.Trim(), ArgumentRegexPattern,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                gameEvent.Origin.Tell(Syntax);
                return;
            }

            var map = match.Groups[1].Length > 0 ? match.Groups[1].ToString() : match.Groups[2].ToString();
            var gametype = match.Groups[3].Length > 0 ? match.Groups[3].ToString() : match.Groups[4].ToString();

            var matchingMaps = gameEvent.Owner.FindMap(map);
            var matchingGametypes = _defaultSettings.FindGametype(gametype, gameEvent.Owner.GameName);

            if (matchingMaps.Count > 1)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_MAG_MULTIPLE_MAPS"]);

                foreach (var matchingMap in matchingMaps)
                {
                    gameEvent.Origin.Tell(
                        $"[(Color::Yellow){matchingMap.Alias}(Color::White)] [(Color::Yellow){matchingMap.Name}(Color::White)]");
                }

                return;
            }

            if (matchingGametypes.Count > 1)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_MAG_MULTIPLE_GAMETYPES"]);

                foreach (var matchingGametype in matchingGametypes)
                {
                    gameEvent.Origin.Tell(
                        $"[(Color::Yellow){matchingGametype.Alias}(Color::White)] [(Color::Yellow){matchingGametype.Name}(Color::White)]");
                }

                return;
            }

            map = matchingMaps.FirstOrDefault()?.Name ?? map;
            gametype = matchingGametypes.FirstOrDefault()?.Name ?? gametype;
            var hasMatchingGametype = matchingGametypes.Any();

            _logger.LogDebug("Changing map to {Map} and gametype {Gametype}", map, gametype);

            await gameEvent.Owner.SetDvarAsync("g_gametype", gametype, gameEvent.Owner.Manager.CancellationToken);
            gameEvent.Owner.Broadcast(_translationLookup["COMMANDS_MAP_SUCCESS"].FormatExt(map));
            await Task.Delay(gameEvent.Owner.Manager.GetApplicationSettings().Configuration().MapChangeDelaySeconds);

            switch (gameEvent.Owner.GameName)
            {
                case Server.Game.IW5:
                    await gameEvent.Owner.ExecuteCommandAsync(
                        $"load_dsr {(hasMatchingGametype ? gametype.ToUpper() + "_default" : gametype)}");
                    await gameEvent.Owner.ExecuteCommandAsync($"map {map}");
                    break;
                case Server.Game.T6:
                    await gameEvent.Owner.ExecuteCommandAsync($"exec {gametype}.cfg");
                    await gameEvent.Owner.ExecuteCommandAsync($"map {map}");
                    break;
                default:
                    await gameEvent.Owner.ExecuteCommandAsync($"map {map}");
                    break;
            }
        }
    }
}
