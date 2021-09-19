using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
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
        private readonly ApplicationConfiguration _appConfig;

        public ReceivedPenaltyResourceQueryHelper(ILogger<ReceivedPenaltyResourceQueryHelper> logger, 
            IDatabaseContextFactory contextFactory, ApplicationConfiguration appConfig)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _appConfig = appConfig;
        }

        public async Task<ResourceQueryHelperResult<ReceivedPenaltyResponse>> QueryResource(ClientPaginationRequest query)
        {
            var linkedPenaltyType = Utilities.LinkedPenaltyTypes();
            await using var ctx = _contextFactory.CreateContext(enableTracking: false);

            var linkId = await ctx.Clients.AsNoTracking()
                    .Where(_client => _client.ClientId == query.ClientId)
                    .Select(_client => new {_client.AliasLinkId, _client.CurrentAliasId })
                    .FirstOrDefaultAsync();

            var iqPenalties = ctx.Penalties.AsNoTracking()
                .Where(_penalty => _penalty.OffenderId == query.ClientId ||
                                   linkedPenaltyType.Contains(_penalty.Type) && _penalty.LinkId == linkId.AliasLinkId);

            IQueryable<EFPenalty> iqIpLinkedPenalties = null;
            
            if (!_appConfig.EnableImplicitAccountLinking)
            {
                var usedIps = await ctx.Aliases.AsNoTracking()
                    .Where(alias => (alias.LinkId == linkId.AliasLinkId || alias.AliasId == linkId.CurrentAliasId) && alias.IPAddress != null)
                    .Select(alias => alias.IPAddress).ToListAsync();

                var aliasedIds = await ctx.Aliases.AsNoTracking().Where(alias => usedIps.Contains(alias.IPAddress))
                    .Select(alias => alias.LinkId)
                    .ToListAsync();

                iqIpLinkedPenalties = ctx.Penalties.AsNoTracking()
                    .Where(penalty =>
                        linkedPenaltyType.Contains(penalty.Type) && aliasedIds.Contains(penalty.LinkId));
            }

            var iqAllPenalties = iqPenalties;
            
            if (iqIpLinkedPenalties != null)
            {
                iqAllPenalties = iqPenalties.Union(iqIpLinkedPenalties);
            }

             var penalties = await iqAllPenalties
                .Where(_penalty => _penalty.When < query.Before)
                .OrderByDescending(_penalty => _penalty.When)
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
