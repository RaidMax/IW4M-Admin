using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore;
using System.Collections.Generic;
using IW4MAdmin.Plugins.Stats.Helpers;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class TopStats : Command
    {
        public static async Task<List<string>> GetTopStats(Server s, ITranslationLookup translationLookup)
        {
            var serverId = StatManager.GetIdForServer(s);
            var topStatsText = new List<string>()
            {
                $"(Color::Accent)--{translationLookup["PLUGINS_STATS_COMMANDS_TOP_TEXT"]}--"
            };

            var stats = await Plugin.Manager.GetTopStats(0, 5, serverId);
            var statsList = stats.Select((stats, index) =>
                translationLookup["COMMANDS_TOPSTATS_RESULT"]
                    .FormatExt(index + 1, stats.Name, stats.KDR, stats.Performance));

            topStatsText.AddRange(statsList);

            // no one qualified
            if (topStatsText.Count == 1)
            {
                topStatsText = new List<string>()
                {
                    translationLookup["PLUGINS_STATS_TEXT_NOQUALIFY"]
                };
            }

            return topStatsText;
        }

        private new readonly CommandConfiguration _config;

        public TopStats(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "topstats";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_TOP_DESC"];
            Alias = "ts";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;

            _config = config;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var topStats = await GetTopStats(gameEvent.Owner, _translationLookup);
            if (!gameEvent.Message.IsBroadcastCommand(_config.BroadcastCommandPrefix))
            {
                await gameEvent.Origin.TellAsync(topStats, gameEvent.Owner.Manager.CancellationToken);
            }
            else
            {
                foreach (var stat in topStats)
                {
                    await gameEvent.Owner.Broadcast(stat).WaitAsync(Utilities.DefaultCommandTimeout,
                        gameEvent.Owner.Manager.CancellationToken);
                }
            }
        }
    }
}
