using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Stats.Helpers
{
    /// <summary>
    /// implementation for IResourceQueryHelper
    /// used to obtain client statistics information
    /// </summary>
    public class StatsResourceQueryHelper : IResourceQueryHelper<StatsInfoRequest, StatsInfoResult>
    {
        private readonly IDatabaseContextFactory _contextFactory;

        public StatsResourceQueryHelper(IDatabaseContextFactory databaseContextFactory)
        {
            _contextFactory = databaseContextFactory;
        }

        /// <inheritdoc/>
        public async Task<ResourceQueryHelperResult<StatsInfoResult>> QueryResource(StatsInfoRequest query)
        {
            var result = new ResourceQueryHelperResult<StatsInfoResult>();
            await using var context = _contextFactory.CreateContext(enableTracking: false);

            // we need to get the ratings separately because there's not explicit FK
            var ratings = await context.Set<EFClientRatingHistory>()
                .Where(_ratingHistory => _ratingHistory.ClientId == query.ClientId)
                .SelectMany(_ratingHistory => _ratingHistory.Ratings.Where(_rating => _rating.ServerId != null && _rating.Newest)
                .Select(_rating => new
                {
                    _rating.ServerId,
                    _rating.Ranking,
                    _rating.When
                }))
                .ToListAsync();

            var iqStats = context.Set<EFClientStatistics>()
                .Where(_stats => _stats.ClientId == query.ClientId)
                .Select(_stats => new StatsInfoResult
                {
                    Name = _stats.Client.CurrentAlias.Name, 
                    ServerId = _stats.ServerId,
                    Kills = _stats.Kills,
                    Deaths = _stats.Deaths,
                    Performance = Math.Round((_stats.EloRating + _stats.Skill) / 2.0, 2),
                    ScorePerMinute = _stats.SPM,
                    LastPlayed = _stats.Client.LastConnection,
                    TotalSecondsPlayed = _stats.TimePlayed,
                    ServerGame = _stats.Server.GameName.ToString(),
                    ServerName = _stats.Server.HostName,
                });

            var queryResults = await iqStats.ToListAsync();

            // add the rating query's results to the full query
            foreach(var eachResult in queryResults)
            {
                var rating = ratings.FirstOrDefault(_rating => _rating.ServerId == eachResult.ServerId);
                eachResult.Ranking = rating?.Ranking ?? 0;
                eachResult.LastPlayed = rating?.When ?? eachResult.LastPlayed;
            }

            result.Results = queryResults;
            result.RetrievedResultCount = queryResults.Count;
            result.TotalResultCount = result.RetrievedResultCount;

            return result;
        }
    }
}
