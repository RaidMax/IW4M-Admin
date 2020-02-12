using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Database.Models;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewComponents
{
    public class PenaltyListViewComponent : ViewComponent
    {
        private const int PENALTY_COUNT = 15;

        public async Task<IViewComponentResult> InvokeAsync(int offset, EFPenalty.PenaltyType showOnly, bool ignoreAutomated)
        {
            var penalties = await Program.Manager.GetPenaltyService().GetRecentPenalties(PENALTY_COUNT, offset, showOnly, ignoreAutomated);
            penalties = User.Identity.IsAuthenticated ? penalties : penalties.Where(p => !p.Sensitive).ToList();

            return View("_List", penalties);
        }
    }
}
