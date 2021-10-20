using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Meta
{
    public class
        ConnectionsResourceQueryHelper : IResourceQueryHelper<ClientPaginationRequest, ConnectionHistoryResponse>
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;

        public ConnectionsResourceQueryHelper(ILogger<ConnectionsResourceQueryHelper> logger,
            IDatabaseContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<ResourceQueryHelperResult<ConnectionHistoryResponse>> QueryResource(
            ClientPaginationRequest query)
        {
            _logger.LogDebug("{Class} {@Request}", nameof(ConnectionsResourceQueryHelper), query);

            await using var context = _contextFactory.CreateContext(enableTracking: false);

            var iqConnections = context.ConnectionHistory.AsNoTracking()
                .Where(history => query.ClientId == history.ClientId)
                .Where(history => history.CreatedDateTime < query.Before)
                .OrderByDescending(history => history.CreatedDateTime);

            var connections = await iqConnections.Select(history => new ConnectionHistoryResponse
                {
                    MetaId = history.ClientConnectionId,
                    ClientId = history.ClientId,
                    Type = MetaType.ConnectionHistory,
                    ShouldDisplay = true,
                    When = history.CreatedDateTime,
                    ServerName = history.Server.HostName,
                    ConnectionType = history.ConnectionType
                })
                .ToListAsync();

            _logger.LogDebug("{Class} retrieved {Number} items", nameof(ConnectionsResourceQueryHelper),
                connections.Count);

            return new ResourceQueryHelperResult<ConnectionHistoryResponse>
            {
                Results = connections
            };
        }
    }
}