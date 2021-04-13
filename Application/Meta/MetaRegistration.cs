using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Meta
{
    public class MetaRegistration : IMetaRegistration
    {
        private readonly ILogger _logger;
        private ITranslationLookup _transLookup;
        private readonly IMetaService _metaService;
        private readonly IEntityService<EFClient> _clientEntityService;
        private readonly IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse> _receivedPenaltyHelper;
        private readonly IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse> _administeredPenaltyHelper;
        private readonly IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse> _updatedAliasHelper;

        public MetaRegistration(ILogger<MetaRegistration> logger, IMetaService metaService, ITranslationLookup transLookup, IEntityService<EFClient> clientEntityService,
            IResourceQueryHelper<ClientPaginationRequest, ReceivedPenaltyResponse> receivedPenaltyHelper,
            IResourceQueryHelper<ClientPaginationRequest, AdministeredPenaltyResponse> administeredPenaltyHelper,
            IResourceQueryHelper<ClientPaginationRequest, UpdatedAliasResponse> updatedAliasHelper)
        {
            _logger = logger;
            _transLookup = transLookup;
            _metaService = metaService;
            _clientEntityService = clientEntityService;
            _receivedPenaltyHelper = receivedPenaltyHelper;
            _administeredPenaltyHelper = administeredPenaltyHelper;
            _updatedAliasHelper = updatedAliasHelper;
        }

        public void Register()
        {
            _metaService.AddRuntimeMeta<ClientPaginationRequest, InformationResponse>(MetaType.Information, GetProfileMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, ReceivedPenaltyResponse>(MetaType.ReceivedPenalty, GetReceivedPenaltiesMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, AdministeredPenaltyResponse>(MetaType.Penalized, GetAdministeredPenaltiesMeta);
            _metaService.AddRuntimeMeta<ClientPaginationRequest, UpdatedAliasResponse>(MetaType.AliasUpdate, GetUpdatedAliasMeta);
        }

        private async Task<IEnumerable<InformationResponse>> GetProfileMeta(ClientPaginationRequest request)
        {
            var metaList = new List<InformationResponse>();
            var lastMapMeta = await _metaService.GetPersistentMeta("LastMapPlayed", new EFClient() { ClientId = request.ClientId });

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
                    Column = 1,
                    Order = 6
                });
            }

            var lastServerMeta = await _metaService.GetPersistentMeta("LastServerPlayed", new EFClient() { ClientId = request.ClientId });

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
                    Column = 0,
                    Order = 6
                });
            }

            var client = await _clientEntityService.Get(request.ClientId);

            if (client == null)
            {
                _logger.LogWarning("No client found with id {clientId} when generating profile meta", request.ClientId);
                return metaList;
            }

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = _transLookup["WEBFRONT_PROFILE_META_PLAY_TIME"],
                Value = TimeSpan.FromHours(client.TotalConnectionTime / 3600.0).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Column = 1,
                Order = 0,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = _transLookup["WEBFRONT_PROFILE_META_FIRST_SEEN"],
                Value = (DateTime.UtcNow - client.FirstConnection).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Column = 1,
                Order = 1,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = _transLookup["WEBFRONT_PROFILE_META_LAST_SEEN"],
                Value = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
                ShouldDisplay = true,
                Column = 1,
                Order = 2,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_CONNECTIONS"],
                Value = client.Connections.ToString("#,##0", new System.Globalization.CultureInfo(Utilities.CurrentLocalization.LocalizationName)),
                ShouldDisplay = true,
                Column = 1,
                Order = 3,
                Type = MetaType.Information
            });

            metaList.Add(new InformationResponse()
            {
                ClientId = client.ClientId,
                Key = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_MASKED"],
                Value = client.Masked ? Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_TRUE"] : Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_CLIENT_META_FALSE"],
                IsSensitive = true,
                Column = 1,
                Order = 4,
                Type = MetaType.Information
            });

            return metaList;
        }

        private async Task<IEnumerable<ReceivedPenaltyResponse>> GetReceivedPenaltiesMeta(ClientPaginationRequest request)
        {
            var penalties = await _receivedPenaltyHelper.QueryResource(request);
            return penalties.Results;
        }

        private async Task<IEnumerable<AdministeredPenaltyResponse>> GetAdministeredPenaltiesMeta(ClientPaginationRequest request)
        {
            var penalties = await _administeredPenaltyHelper.QueryResource(request);
            return penalties.Results;
        }

        private async Task<IEnumerable<UpdatedAliasResponse>> GetUpdatedAliasMeta(ClientPaginationRequest request)
        {
            var aliases = await _updatedAliasHelper.QueryResource(request);
            return aliases.Results;
        }
    }
}
