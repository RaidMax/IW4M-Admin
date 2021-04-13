using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Meta
{
    /// <summary>
    /// implementation of IResourceQueryHelper
    /// used to pull in penalties applied to a given client id
    /// </summary>
    public class ReceivedPenaltyResourceQueryHelper : IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse>
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;

        public ReceivedPenaltyResourceQueryHelper(ILogger<ReceivedPenaltyResourceQueryHelper> logger, IDatabaseContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<ResourceQueryHelperResult<ReceivedPenaltyResponse>> QueryResource(ClientPaginationRequest query)
        {
            var linkedPenaltyType = Utilities.LinkedPenaltyTypes();
            await using var ctx = _contextFactory.CreateContext(enableTracking: false);

            var linkId = await ctx.Clients.AsNoTracking()
                    .Where(_client => _client.ClientId == query.ClientId)
                    .Select(_client => _client.AliasLinkId)
                    .FirstOrDefaultAsync();

            var iqPenalties = ctx.Penalties.AsNoTracking()
                .Where(_penalty => _penalty.OffenderId == query.ClientId || (linkedPenaltyType.Contains(_penalty.Type) && _penalty.LinkId == linkId))
                .Where(_penalty => _penalty.When < query.Before)
                .OrderByDescending(_penalty => _penalty.When);

            var penalties = await iqPenalties
                .Take(query.Count)
                .Select(_penalty => new ReceivedPenaltyResponse()
                {
                    PenaltyId = _penalty.PenaltyId,
                    ClientId = query.ClientId,
                    Offense = _penalty.Offense,
                    AutomatedOffense = _penalty.AutomatedOffense,
                    OffenderClientId = _penalty.OffenderId,
                    OffenderName = _penalty.Offender.CurrentAlias.Name,
                    PunisherClientId = _penalty.PunisherId,
                    PunisherName = _penalty.Punisher.CurrentAlias.Name,
                    PenaltyType = _penalty.Type,
                    When = _penalty.When,
                    ExpirationDate = _penalty.Expires,
                    IsLinked = _penalty.OffenderId != query.ClientId,
                    IsSensitive = _penalty.Type == EFPenalty.PenaltyType.Flag
                })
                .ToListAsync();

            return new ResourceQueryHelperResult<ReceivedPenaltyResponse>
            {
                // todo: maybe actually count
                RetrievedResultCount = penalties.Count,
                Results = penalties
            };
        }
    }
}
