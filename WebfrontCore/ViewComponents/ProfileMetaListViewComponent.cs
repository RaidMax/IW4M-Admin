using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Services;
using System;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class ProfileMetaListViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int clientId, int count, int offset, DateTime? startAt)
        {
            var meta = await MetaService.GetRuntimeMeta(clientId, offset, count, startAt ?? DateTime.UtcNow);
            return View("_List", meta);
        }
    }
}
