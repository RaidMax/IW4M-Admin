using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Meta
{
    /// <summary>
    /// implementation if IResrouceQueryHerlp
    /// used to pull alias changes for given client id
    /// </summary>
    public class UpdatedAliasResourceQueryHelper : IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse>
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;

        public UpdatedAliasResourceQueryHelper(ILogger<UpdatedAliasResourceQueryHelper> logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        public async Task<ResourceQueryHelperResult<UpdatedAliasResponse>> QueryResource(ClientPaginationRequest query)
        {
            await using var ctx = _contextFactory.CreateContext(enableTracking: false);
            int linkId = ctx.Clients.First(_client => _client.ClientId == query.ClientId).AliasLinkId;

            var iqAliasUpdates = ctx.Aliases
                .Where(_alias => _alias.LinkId == linkId)
                .Where(_alias => _alias.DateAdded < query.Before)
                .Where(_alias => _alias.IPAddress != null)
                .OrderByDescending(_alias => _alias.DateAdded)
                .Select(_alias => new UpdatedAliasResponse
                {
                    MetaId = _alias.AliasId,
                    Name = _alias.Name,
                    IPAddress = _alias.IPAddress.ConvertIPtoString(),
                    When = _alias.DateAdded,
                    Type = MetaType.AliasUpdate,
                    IsSensitive = true
                });

            var result = (await iqAliasUpdates
                .Take(query.Count)
                .ToListAsync())
                .Distinct();


            return new ResourceQueryHelperResult<UpdatedAliasResponse>
            {
                Results = result, // we can potentially have duplicates
                RetrievedResultCount = result.Count()
            };
        }
    }
}
