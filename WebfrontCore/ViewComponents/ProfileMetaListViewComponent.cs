using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Services;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class ProfileMetaListViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int clientId, int count, int offset)
        {
            var meta = await MetaService.GetRuntimeMeta(clientId, offset, count);
            return View("_List", meta);
        }
    }
}
