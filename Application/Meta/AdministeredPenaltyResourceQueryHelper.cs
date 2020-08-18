using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;

namespace IW4MAdmin.Application.Meta
{
    /// <summary>
    /// implementation of IResourceQueryHelper
    /// query helper that retrieves administered penalties for provided client id
    /// </summary>
    public class AdministeredPenaltyResourceQueryHelper : IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse>
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;

        public AdministeredPenaltyResourceQueryHelper(ILogger logger, IDatabaseContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<ResourceQueryHelperResult<AdministeredPenaltyResponse>> QueryResource(ClientPaginationRequest query)
        {
            using var ctx = _contextFactory.CreateContext(enableTracking: false);

            var iqPenalties = ctx.Penalties.AsNoTracking()
                .Where(_penalty => query.ClientId == _penalty.PunisherId)
                .Where(_penalty => _penalty.When < query.Before)
                .OrderByDescending(_penalty => _penalty.When);

            var penalties = await iqPenalties
                .Take(query.Count)
                .Select(_penalty => new AdministeredPenaltyResponse()
                {
                    PenaltyId = _penalty.PenaltyId,
                    Offense = _penalty.Offense,
                    AutomatedOffense = _penalty.AutomatedOffense,
                    ClientId = _penalty.OffenderId,
                    OffenderName = _penalty.Offender.CurrentAlias.Name,
                    OffenderClientId = _penalty.Offender.ClientId,
                    PunisherClientId = _penalty.PunisherId,
                    PunisherName = _penalty.Punisher.CurrentAlias.Name,
                    PenaltyType = _penalty.Type,
                    When = _penalty.When,
                    ExpirationDate = _penalty.Expires,
                    IsLinked = _penalty.OffenderId != query.ClientId,
                    IsSensitive = _penalty.Type == EFPenalty.PenaltyType.Flag
                })
                .ToListAsync();

            return new ResourceQueryHelperResult<AdministeredPenaltyResponse>
            {
                // todo: might need to do count at some point
                RetrievedResultCount = penalties.Count,
                Results = penalties
            };
        }
    }
}
