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

            if (serverCache == null)
            {
                serverCache = await context.Set<EFServer>().ToListAsync();
            }

            if (int.TryParse(query.ServerId, out int serverId))
            {
                query.ServerId = serverCache.FirstOrDefault(_server => _server.ServerId == serverId)?.EndPoint ?? query.ServerId;
            }

            var iqMessages = context.Set<EFClientMessage>()
                .Where(_message => _message.TimeSent >= query.SentAfter)
                .Where(_message => _message.TimeSent < query.SentBefore);

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
                iqMessages = iqMessages.Where(_message => EF.Functions.Like(_message.Message.ToLower(), $"%{query.MessageContains.ToLower()}%"));
            }

            var iqResponse = iqMessages
                .Select(_message => new MessageResponse
                {
                    ClientId = _message.ClientId,
                    ClientName = query.IsProfileMeta ? "" : _message.Client.CurrentAlias.Name,
                    ServerId = _message.ServerId,
                    When = _message.TimeSent,
                    Message = _message.Message,
                    ServerName = query.IsProfileMeta ? "" : _message.Server.HostName,
                    GameName = _message.Server.GameName == null ? Server.Game.IW4 : (Server.Game)_message.Server.GameName.Value,
                    SentIngame = _message.SentIngame
                });

            if (query.Direction == SharedLibraryCore.Dtos.SortDirection.Descending)
            {
                iqResponse = iqResponse.OrderByDescending(_message => _message.When);
            }

            else
            {
                iqResponse = iqResponse.OrderBy(_message => _message.When);
            }

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
                        .First(_qm => _qm.Game == message.GameName);
                    message.Message = quickMessages.Messages[message.Message.Substring(1)];
                    message.IsQuickMessage = true;
                }
                catch 
                {
                    message.Message = message.Message.Substring(1);
                }
            }

            result.TotalResultCount = await iqResponse.CountAsync();
            result.Results = resultList;
            result.RetrievedResultCount = resultList.Count;

            return result;
        }
    }
}
