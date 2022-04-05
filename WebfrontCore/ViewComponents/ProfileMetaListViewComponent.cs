using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using WebfrontCore.Permissions;

namespace WebfrontCore.ViewComponents
{
    public class ProfileMetaListViewComponent : ViewComponent
    {
        private readonly IMetaServiceV2 _metaService;
        private readonly ApplicationConfiguration _appConfig;

        public ProfileMetaListViewComponent(IMetaServiceV2 metaService, ApplicationConfiguration appConfig)
        {
            _metaService = metaService;
            _appConfig = appConfig;
        }

        public async Task<IViewComponentResult> InvokeAsync(int clientId, int count, int offset, DateTime? startAt, MetaType? metaType, CancellationToken token)
        {
            var level = (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission), UserClaimsPrincipal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User");

            var request = new ClientPaginationRequest
            {
                ClientId = clientId,
                Count = count,
                Offset = offset,
                Before = startAt,
            };

            var meta = await GetClientMeta(_metaService, metaType, level, request, token);
            ViewBag.Localization = SharedLibraryCore.Utilities.CurrentLocalization.LocalizationIndex;

            return View("_List", meta);
        }

        private async Task<IEnumerable<IClientMeta>> GetClientMeta(IMetaServiceV2 metaService, MetaType? metaType,
            EFClient.Permission level, ClientPaginationRequest request, CancellationToken token)
        {
            IEnumerable<IClientMeta> meta = null;

            if (!_appConfig.PermissionSets.TryGetValue(level.ToString(), out var permissionSet))
            {
                permissionSet = new List<string>();
            }
            
            if (metaType is null or MetaType.All)
            {
                meta = await metaService.GetRuntimeMeta(request, token);
            }

            else
            {
                meta = metaType switch
                {
                    MetaType.Information => await metaService.GetRuntimeMeta<InformationResponse>(request,
                        metaType.Value, token),
                    MetaType.AliasUpdate => permissionSet.HasPermission(WebfrontEntity.MetaAliasUpdate, WebfrontPermission.Read) ? await metaService.GetRuntimeMeta<UpdatedAliasResponse>(request,
                        metaType.Value, token) : new List<IClientMeta>(),
                    MetaType.ChatMessage => await metaService.GetRuntimeMeta<MessageResponse>(request, metaType.Value,
                        token),
                    MetaType.Penalized => await metaService.GetRuntimeMeta<AdministeredPenaltyResponse>(request,
                        metaType.Value, token),
                    MetaType.ReceivedPenalty => await metaService.GetRuntimeMeta<ReceivedPenaltyResponse>(request,
                        metaType.Value, token),
                    MetaType.ConnectionHistory => await metaService.GetRuntimeMeta<ConnectionHistoryResponse>(request,
                        metaType.Value, token),
                    MetaType.PermissionLevel => await metaService.GetRuntimeMeta<PermissionLevelChangedResponse>(
                        request, metaType.Value, token),
                    _ => meta
                };
            }

            if (level < EFClient.Permission.Trusted)
            {
                meta = meta.Where(_meta => !_meta.IsSensitive);
            }

            return meta;
        }
    }
}
