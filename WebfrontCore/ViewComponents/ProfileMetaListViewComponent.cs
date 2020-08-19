using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class ProfileMetaListViewComponent : ViewComponent
    {
        private readonly IMetaService _metaService;

        public ProfileMetaListViewComponent(IMetaService metaService)
        {
            _metaService = metaService;
        }

        public async Task<IViewComponentResult> InvokeAsync(int clientId, int count, int offset, DateTime? startAt, MetaType? metaType)
        {
            var level = (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission), UserClaimsPrincipal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User");

            var request = new ClientPaginationRequest
            {
                ClientId = clientId,
                Count = count,
                Offset = offset,
                Before = startAt,
            };

            var meta = await GetClientMeta(_metaService, metaType, level, request);
            ViewBag.Localization = SharedLibraryCore.Utilities.CurrentLocalization.LocalizationIndex;

            return View("_List", meta);
        }

        public static async Task<IEnumerable<IClientMeta>> GetClientMeta(IMetaService metaService, MetaType? metaType, EFClient.Permission level, ClientPaginationRequest request)
        {
            IEnumerable<IClientMeta> meta = null;

            if (metaType == null) // all types
            {
                meta = await metaService.GetRuntimeMeta(request);
            }

            else
            {
                switch (metaType)
                {
                    case MetaType.Information:
                        meta = await metaService.GetRuntimeMeta<InformationResponse>(request, metaType.Value);
                        break;
                    case MetaType.AliasUpdate:
                        meta = await metaService.GetRuntimeMeta<UpdatedAliasResponse>(request, metaType.Value);
                        break;
                    case MetaType.ChatMessage:
                        meta = await metaService.GetRuntimeMeta<MessageResponse>(request, metaType.Value);
                        break;
                    case MetaType.Penalized:
                        meta = await metaService.GetRuntimeMeta<AdministeredPenaltyResponse>(request, metaType.Value);
                        break;
                    case MetaType.ReceivedPenalty:
                        meta = await metaService.GetRuntimeMeta<ReceivedPenaltyResponse>(request, metaType.Value);
                        break;
                    default:
                        break;
                }
            }

            if (level < EFClient.Permission.Trusted)
            {
                meta = meta.Where(_meta => !_meta.IsSensitive);
            }

            return meta;
        }
    }
}
