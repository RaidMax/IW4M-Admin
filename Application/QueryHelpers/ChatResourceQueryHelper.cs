using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;

namespace IW4MAdmin.Application.QueryHelpers;

/// <summary>
/// implementation of IResourceQueryHelper
/// </summary>
public class ChatResourceQueryHelper(
    IDatabaseContextFactory contextFactory,
    DefaultSettings defaultSettings)
    : IResourceQueryHelper<ChatSearchQuery, MessageResponse>
{
    private List<EFServer>? _serverCache;

    /// <inheritdoc/>
    public async Task<ResourceQueryHelperResult<MessageResponse>> QueryResource(ChatSearchQuery query)
    {
        if (query == null)
        {
            throw new ArgumentException("Query must be specified");
        }

        var result = new ResourceQueryHelperResult<MessageResponse>();
        await using var context = contextFactory.CreateContext(enableTracking: false);

        _serverCache ??= await context.Set<EFServer>().ToListAsync();

        if (int.TryParse(query.ServerId, out var serverId))
        {
            query.ServerId = _serverCache.FirstOrDefault(server => server.ServerId == serverId)?.EndPoint ??
                             query.ServerId;
        }

        var iqMessages = context.Set<EFClientMessage>()
            .Where(message => message.TimeSent < query.SentBeforeDateTime);

        if (query.SentAfterDateTime is not null)
        {
            iqMessages = iqMessages.Where(message => message.TimeSent >= query.SentAfterDateTime);
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
            iqMessages = query.IsExactMatch
                ? iqMessages.Where(message => message.Message.ToLower() == query.MessageContains.ToLower())
                : iqMessages.Where(message =>
                    EF.Functions.Like(message.Message.ToLower(), $"%{query.MessageContains.ToLower()}%"));
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
                GameName = message.Server.GameName == null
                    ? Server.Game.IW4
                    : (Server.Game)message.Server.GameName.Value,
                SentIngame = message.SentIngame,
                IsHidden = message.Server.IsPasswordProtected,
                Type = MetaType.ChatMessage
            });

        iqResponse = query.Direction == SharedLibraryCore.Dtos.SortDirection.Descending
            ? iqResponse.OrderByDescending(message => message.When)
            : iqResponse.OrderBy(message => message.When);

        var total = 0;
        if (query.Offset == 0)
        {
            total = await iqResponse.CountAsync();
        }

        var resultList = await iqResponse
            .Skip(query.Offset)
            .Take(query.Count)
            .ToListAsync();

        foreach (var message in resultList)
        {
            if (message.IsHidden && !query.IsPrivileged)
            {
                message.Message = message.HiddenMessage;
            }

            if (!message.Message.IsQuickMessage())
            {
                continue;
            }

            try
            {
                var quickMessages = defaultSettings
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

        result.TotalResultCount = total;
        result.Results = resultList;
        result.RetrievedResultCount = resultList.Count;

        return result;
    }
}
