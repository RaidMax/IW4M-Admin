using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class ProfileMetaListViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int clientId, int count, int offset, DateTime? startAt)
        {
            var level = (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission), UserClaimsPrincipal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User");

            IEnumerable<ProfileMeta> meta = await MetaService.GetRuntimeMeta(clientId, offset, count, startAt ?? DateTime.UtcNow);

            if (level < EFClient.Permission.Trusted)
            {
                meta = meta.Where(_meta => !_meta.Sensitive);
            }

            return View("_List", meta);
        }
    }
}
