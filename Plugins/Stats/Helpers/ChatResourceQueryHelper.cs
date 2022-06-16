using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stats.Helpers
{
    /// <summary>
    /// implementation of IResourceQueryHelper
    /// </summary>
    public class ChatResourceQueryHelper : IResourceQueryHelper<ChatSearchQuery, MessageResponse>
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;
        private readonly DefaultSettings _defaultSettings;
        private List<EFServer> serverCache;

        public ChatResourceQueryHelper(ILogger<ChatResourceQueryHelper> logger, IDatabaseContextFactory contextFactory, DefaultSettings defaultSettings)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _defaultSettings = defaultSettings;
        }

        /// <inheritdoc/>
        public async Task<ResourceQueryHelperResult<MessageResponse>> QueryResource(ChatSearchQuery query)
        {
            if (query == null)
            {
                throw new ArgumentException("Query must be specified");
            }

            var result = new ResourceQueryHelperResult<MessageResponse>();
            await using var context = _contextFactory.CreateContext(enableTracking: false);

            serverCache ??= await context.Set<EFServer>().ToListAsync();

            if (int.TryParse(query.ServerId, out var serverId))
            {
                query.ServerId = serverCache.FirstOrDefault(server => server.ServerId == serverId)?.EndPoint ?? query.ServerId;
            }

            var iqMessages = context.Set<EFClientMessage>()
                .Where(message => message.TimeSent < query.SentBefore);

            if (query.SentAfter is not null)
            {
                iqMessages = iqMessages.Where(message => message.TimeSent >= query.SentAfter);
            }

            if (query.ClientId is not null)
            {
                iqMessages = iqMessages.Where(message => message.ClientId == query.ClientId.Value);
            }

            if (query.ServerId is not null)
            {
                iqMessages = iqMessages.Where(message => message.Server.EndPoint == query.ServerId);
            }

            if (!string.IsNullOrEmpty(query.MessageContains))
            {
                iqMessages = iqMessages.Where(message => EF.Functions.Like(message.Message.ToLower(), $"%{query.MessageContains.ToLower()}%"));
            }

            var iqResponse = iqMessages
                .Select(message => new MessageResponse
                {
                    ClientId = message.ClientId,
                    ClientName = query.IsProfileMeta ? "" : message.Client.CurrentAlias.Name,
                    ServerId = message.ServerId,
                    When = message.TimeSent,
                    Message = message.Message,
                    ServerName = query.IsProfileMeta ? "" : message.Server.HostName,
                    GameName = message.Server.GameName == null ? Server.Game.IW4 : (Server.Game)message.Server.GameName.Value,
                    SentIngame = message.SentIngame
                });

            iqResponse = query.Direction == SharedLibraryCore.Dtos.SortDirection.Descending
                ? iqResponse.OrderByDescending(message => message.When)
                : iqResponse.OrderBy(message => message.When);
                
            var resultList = await iqResponse
                .Skip(query.Offset)
                .Take(query.Count)
                .ToListAsync();

            foreach (var message in resultList)
            {
                message.IsHidden = serverCache.Any(server => server.ServerId == message.ServerId && server.IsPasswordProtected);

                if (!message.Message.IsQuickMessage())
                {
                    continue;
                }
                
                try
                {
                    var quickMessages = _defaultSettings
                        .QuickMessages
                        .First(qm => qm.Game == message.GameName);
                    message.Message = quickMessages.Messages[message.Message.Substring(1)];
                    message.IsQuickMessage = true;
                }
                catch 
                {
                    message.Message = message.Message[1..];
                }
            }

            result.TotalResultCount = await iqResponse.CountAsync();
            result.Results = resultList;
            result.RetrievedResultCount = resultList.Count;

            return result;
        }
    }
}
