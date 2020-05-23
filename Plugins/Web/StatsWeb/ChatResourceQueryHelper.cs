using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using StatsWeb.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StatsWeb
{
    /// <summary>
    /// implementation of IResourceQueryHelper
    /// </summary>
    public class ChatResourceQueryHelper : IResourceQueryHelper<ChatSearchQuery, ChatSearchResult>
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public ChatResourceQueryHelper(ILogger logger, IDatabaseContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ResourceQueryHelperResult<ChatSearchResult>> QueryResource(ChatSearchQuery query)
        {
            if (query == null)
            {
                throw new ArgumentException("Query must be specified");
            }

            var result = new ResourceQueryHelperResult<ChatSearchResult>();
            using var context = _contextFactory.CreateContext(enableTracking: false);
            
            var iqMessages = context.Set<EFClientMessage>()
                .Where(_message => _message.TimeSent >= query.SentAfter)
                .Where(_message => _message.TimeSent <= query.SentBefore);

            if (query.ClientId != null)
            {
                iqMessages = iqMessages.Where(_message => _message.ClientId == query.ClientId.Value);
            }

            if (query.ServerId != null)
            {
                iqMessages = iqMessages.Where(_message => _message.Server.EndPoint == query.ServerId);
            }

            if (!string.IsNullOrEmpty(query.MessageContains))
            {
                iqMessages = iqMessages.Where(_message => EF.Functions.Like(_message.Message, $"%{query.MessageContains}%"));
            }

            var iqResponse = iqMessages
                .Select(_message => new ChatSearchResult
                {
                    ClientId = _message.ClientId,
                    ClientName = _message.Client.CurrentAlias.Name,
                    Date = _message.TimeSent,
                    Message = _message.Message,
                    ServerName = _message.Server.HostName
                });

            if (query.Direction == SharedLibraryCore.Dtos.SortDirection.Descending)
            {
                iqResponse = iqResponse.OrderByDescending(_message => _message.Date);
            }

            else
            {
                iqResponse = iqResponse.OrderBy(_message => _message.Date);
            }

            var resultList = await iqResponse
                .Skip(query.Offset)
                .Take(query.Count)
                .ToListAsync();

            result.TotalResultCount = await iqResponse.CountAsync();
            result.Results = resultList;
            result.RetrievedResultCount = resultList.Count;

            return result;
        }
    }
}
