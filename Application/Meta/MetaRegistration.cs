using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Meta
{
    public class MetaRegistration : IMetaRegistration
    {
        private readonly ILogger _logger;
        private ITranslationLookup _transLookup;
        private readonly IMetaServiceV2 _metaService;
        private readonly IEntityService<EFClient> _clientEntityService;
        private readonly IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse> _receivedPenaltyHelper;

        private readonly IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse>
            _administeredPenaltyHelper;

        private readonly IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse> _updatedAliasHelper;

        private readonly IResourceQueryHelper<ClientPaginationRequest, ConnectionHistoryResponse>
            _connectionHistoryHelper;

        private readonly IResourceQueryHelper<ClientPaginationRequest, PermissionLevelChangedResponse>
            _permissionLevelHelper;

        public MetaRegistration(ILogger<MetaRegistration> logger, IMetaServiceV2 metaService,
            ITranslationLookup transLookup, IEntityService<EFClient> clientEntityService,
            IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse> receivedPenaltyHelper,
            IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse> administeredPenaltyHelper,
            IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse> updatedAliasHelper,
            IResourceQueryHelper<ClientPaginationRequest, ConnectionHistoryResponse> connectionHistoryHelper,
            IResourceQueryHelper<ClientPaginationRequest, PermissionLevelChangedResponse> permissionLevelHelper)
        {
            _logger = logger;
            _transLookup = transLookup;
            _metaService = metaService;
            _clientEntityService = clientEntityService;
            _receivedPenaltyHelper = receivedPenaltyHelper;
            _administeredPenaltyHelper = administeredPenaltyHelper;
            _updatedAliasHelper = updatedAliasHelper;
            _connectionHistoryHelper = connectionHistoryHelper;
            _permissionLevelHelper = permissionLevelHelper;
        }

        public void Register()
        {
            _metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information,
                GetProfileMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, ReceivedPenaltyResponse>(MetaType.ReceivedPenalty,
                GetReceivedPenaltiesMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, AdministeredPenaltyResponse>(MetaType.Penalized,
                GetAdministeredPenaltiesMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, UpdatedAliasResponse>(MetaType.AliasUpdate,
                GetUpdatedAliasMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, ConnectionHistoryResponse>(MetaType.ConnectionHistory,
                GetConnectionHistoryMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, PermissionLevelChangedResponse>(
                MetaType.PermissionLevel, GetPermissionLevelMeta);
        }

        private async Task<IEnumerable<InformationResponse>> GetProfileMeta(ClientPaginationRequest request,
            CancellationToken cancellationToken = default)
        {
            var metaList = new List<InformationResponse>();
            var lastMapMeta =
                await _metaService.GetPersistentMeta("LastMapPlayed", request.ClientId, cancellationToken);

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
                    Order = 6
                });
            }

            var lastServerMeta =
                await _metaService.GetPersistentMeta("LastServerPlayed", request.ClientId, cancellationToken);

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
                    Order = 7
                });
            }

            var client = await _clientEntityService.Get(request.ClientId);

            if (client == null)
            {
                _logger.LogWarning("No client found with id {ClientId} when generating profile meta", request.ClientId);
                return metaList;
            }

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = _transLookup["WEBFRONT_PROFILE_META_PLAY_TIME"],
                Value = TimeSpan.FromHours(client.TotalConnectionTime / 3600.0).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Order = 8,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = _transLookup["WEBFRONT_PROFILE_META_FIRST_SEEN"],
                Value = (DateTime.UtcNow - client.FirstConnection).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Order = 9,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = _transLookup["WEBFRONT_PROFILE_META_LAST_SEEN"],
                Value = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Order = 10,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_CONNECTIONS"],
                Value = client.Connections.ToString("#,##0",
                    new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                ShouldDisplay = true,
                Order = 11,
                Type = MetaType.Information
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
                Type = MetaType.Information
            });

            return metaList;
        }

        private async Task<IEnumerable<ReceivedPenaltyResponse>> GetReceivedPenaltiesMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var penalties = await _receivedPenaltyHelper.QueryResource(request);
            return penalties.Results;
        }

        private async Task<IEnumerable<AdministeredPenaltyResponse>> GetAdministeredPenaltiesMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var penalties = await _administeredPenaltyHelper.QueryResource(request);
            return penalties.Results;
        }

        private async Task<IEnumerable<UpdatedAliasResponse>> GetUpdatedAliasMeta(ClientPaginationRequest request,
            CancellationToken token = default)
        {
            var aliases = await _updatedAliasHelper.QueryResource(request);
            return aliases.Results;
        }

        private async Task<IEnumerable<ConnectionHistoryResponse>> GetConnectionHistoryMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var connections = await _connectionHistoryHelper.QueryResource(request);
            return connections.Results;
        }

        private async Task<IEnumerable<PermissionLevelChangedResponse>> GetPermissionLevelMeta(
            ClientPaginationRequest request, CancellationToken token = default)
        {
            var permissionChanges = await _permissionLevelHelper.QueryResource(request);
            return permissionChanges.Results;
        }
    }
}
