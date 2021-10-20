using System;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats.Cheat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stats.Commands
{
    public class ResetAnticheatMetricsCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public ResetAnticheatMetricsCommand(ILogger<ResetAnticheatMetricsCommand> logger, CommandConfiguration config,
            ITranslationLookup translationLookup,
            IDatabaseContextFactory contextFactory) : base(config, translationLookup)
        {
            Name = "resetanticheat";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_RESETAC_DESC"];
            Alias = "rsa";
            Permission = EFClient.Permission.Owner;
            RequiresTarget = true;

            _contextFactory = contextFactory;
            _logger = logger;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            try
            {
                var clientDetection =
                    gameEvent.Target.GetAdditionalProperty<Detection>(IW4MAdmin.Plugins.Stats.Helpers.StatManager
                        .CLIENT_DETECTIONS_KEY);
                var clientStats =
                    gameEvent.Target.GetAdditionalProperty<EFClientStatistics>(IW4MAdmin.Plugins.Stats.Helpers
                        .StatManager.CLIENT_STATS_KEY);

                if (clientStats != null)
                {
                    clientStats.MaxStrain = 0;
                    clientStats.AverageSnapValue = 0;
                    clientStats.SnapHitCount = 0;
                    clientStats.HitLocations.Clear();
                }

                clientDetection?.TrackedHits.Clear();

                await using var context = _contextFactory.CreateContext();

                var hitLocationCounts = await context.Set<EFHitLocationCount>()
                    .Where(loc => loc.EFClientStatisticsClientId == gameEvent.Target.ClientId)
                    .Select(loc => new EFHitLocationCount()
                    {
                        HitLocationCountId = loc.HitLocationCountId
                    })
                    .ToListAsync();

                context.RemoveRange(hitLocationCounts);

                await context.SaveChangesAsync();

                var stats = await context.Set<EFClientStatistics>()
                    .Where(stat => stat.ClientId == gameEvent.Target.ClientId)
                    .ToListAsync();

                foreach (var stat in stats)
                {
                    stat.MaxStrain = 0;
                    stat.AverageSnapValue = 0;
                    stat.SnapHitCount = 0;
                }

                context.UpdateRange(stats);
                await context.SaveChangesAsync();

                gameEvent.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_RESETAC_SUCCESS"]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not reset anticheat metrics for {Target}", gameEvent.Target);
                throw;
            }
        }
    }
}