using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.Extensions.Logging;
using Stats.Dtos;
using WebfrontCore.Core.Auth;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Meta
{
    public class MetaRegistration(
        ILogger<MetaRegistration> logger,
        IMetaServiceV2 metaService,
        ITranslationLookup transLookup,
        IEntityService<EFClient> clientEntityService,
        IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse> receivedPenaltyHelper,
        IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse> administeredPenaltyHelper,
        IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse> updatedAliasHelper,
        IResourceQueryHelper<ClientPaginationRequest, ConnectionHistoryResponse> connectionHistoryHelper,
        IResourceQueryHelper<ClientPaginationRequest, PermissionLevelChangedResponse> permissionLevelHelper,
        IResourceQueryHelper<ChatSearchQuery, MessageResponse> chatHelper)
        : IMetaRegistration
    {
        private readonly ILogger _logger = logger;

        public void Register()
        {
            metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information,
                GetProfileMeta);
            metaService.AddRuntimeMeta<ClientPaginationRequest, ReceivedPenaltyResponse>(MetaType.ReceivedPenalty,
                GetReceivedPenaltiesMeta, nameof(WebfrontEntity.Penalty));
            metaService.AddRuntimeMeta<ClientPaginationRequest, AdministeredPenaltyResponse>(MetaType.Penalized,
                GetAdministeredPenaltiesMeta, nameof(WebfrontEntity.Penalty));
            metaService.AddRuntimeMeta<ClientPaginationRequest, UpdatedAliasResponse>(MetaType.AliasUpdate,
                GetUpdatedAliasMeta, nameof(WebfrontEntity.MetaAliasUpdate));
            metaService.AddRuntimeMeta<ClientPaginationRequest, ConnectionHistoryResponse>(MetaType.ConnectionHistory,
                GetConnectionHistoryMeta);
            metaService.AddRuntimeMeta<ClientPaginationRequest, PermissionLevelChangedResponse>(
                MetaType.PermissionLevel, GetPermissionLevelMeta, nameof(WebfrontEntity.ClientLevel));
            metaService.AddRuntimeMeta<ClientPaginationRequest, MessageResponse>(MetaType.ChatMessage, GetChatMessages,
                nameof(WebfrontEntity.ChatMessage));
        }

        private async Task<IEnumerable<InformationResponse>> GetProfileMeta(ClientPaginationRequest request,
            CancellationToken cancellationToken = default)
        {
            var metaList = new List<InformationResponse>();
            var lastMapMeta =
                await metaService.GetPersistentMeta("LastMapPlayed", request.ClientId, cancellationToken);

            if (lastMapMeta != null)
            {
                metaList.Add(new InformationResponse()
                {
                    ClientId = request.ClientId,
                    MetaId = lastMapMeta.MetaId,
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_LAST_MAP"],
                    Value = lastMapMeta.Value,
                    ShouldDisplay = true,
                    Type = MetaType.Information,
                    Order = 6,
                    Category = "General"
                });
            }

            var lastServerMeta =
                await metaService.GetPersistentMeta("LastServerPlayed", request.ClientId, cancellationToken);

            if (lastServerMeta != null)
            {
                metaList.Add(new InformationResponse()
                {
                    ClientId = request.ClientId,
                    MetaId = lastServerMeta.MetaId,
                    Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_LAST_SERVER"],
                    Value = lastServerMeta.Value,
                    ShouldDisplay = true,
                    Type = MetaType.Information,
                    Order = 7,
                    Category = "General"
                });
            }

            var client = await clientEntityService.Get(request.ClientId);

            if (client == null)
            {
                _logger.LogWarning("No client found with id {ClientId} when generating profile meta", request.ClientId);
                return metaList;
            }

            var friendlyTime = TimeSpan.FromHours(client.TotalConnectionTime / 3600.0);
            metaList.Add(new InformationResponse
            {
                ClientId = client.ClientId,
                Key = transLookup["WEBFRONT_PROFILE_META_PLAY_TIME"],
                Value = friendlyTime.HumanizeForCurrentCulture(),
                ToolTipText = friendlyTime.HumanizeForCurrentCulture(maxUnit: TimeUnit.Hour),
                ShouldDisplay = true,
                Order = 8,
                Type = MetaType.Information,
                Category = "General"
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = transLookup["WEBFRONT_PROFILE_META_FIRST_SEEN"],
                Value = (DateTime.UtcNow - client.FirstConnection).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Order = 9,
                Type = MetaType.Information,
                Category = "General"
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = transLookup["WEBFRONT_PROFILE_META_LAST_SEEN"],
                Value = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Order = 10,
                Type = MetaType.Information,
                Category = "General"
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_CONNECTIONS"],
                Value = client.Connections.ToString("#,##0",
                    new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                ShouldDisplay = true,
                Order = 11,
                Type = MetaType.Information,
                Category = "General"
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_MASKED"],
                Value = client.Masked
                    ? Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_TRUE"]
                    : Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_FALSE"],
                IsSensitive = true,
                Order = 12,
                Type = MetaType.Information,
                Category = "General"
            });

            return metaList;
        }

        private async Task<IEnumerable<ReceivedPenaltyResponse>> GetReceivedPenaltiesMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var penalties = await receivedPenaltyHelper.QueryResource(request);
            return penalties.Results;
        }

        private async Task<IEnumerable<AdministeredPenaltyResponse>> GetAdministeredPenaltiesMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var penalties = await administeredPenaltyHelper.QueryResource(request);
            return penalties.Results;
        }

        private async Task<IEnumerable<UpdatedAliasResponse>> GetUpdatedAliasMeta(ClientPaginationRequest request,
            CancellationToken token = default)
        {
            var aliases = await updatedAliasHelper.QueryResource(request);
            return aliases.Results;
        }

        private async Task<IEnumerable<ConnectionHistoryResponse>> GetConnectionHistoryMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var connections = await connectionHistoryHelper.QueryResource(request);
            return connections.Results;
        }

        private async Task<IEnumerable<PermissionLevelChangedResponse>> GetPermissionLevelMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var permissionChanges = await permissionLevelHelper.QueryResource(request);
            return permissionChanges.Results;
        }

        private async Task<IEnumerable<MessageResponse>> GetChatMessages(ClientPaginationRequest request,
            CancellationToken token = default)
        {
            var query = new ChatSearchQuery
            {
                ClientId = request.ClientId,
                Before = request.Before,
                SentBefore = request.Before ?? DateTime.UtcNow,
                SentAfter = request.After,
                After = request.After,
                Count = request.Count,
                IsProfileMeta = true,
                IsPrivileged = request.IsPrivileged
            };

            return (await chatHelper.QueryResource(query)).Results;
        }
    }
}
