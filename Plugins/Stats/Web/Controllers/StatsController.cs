using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebfrontCore.Controllers;

namespace IW4MAdmin.Plugins.Stats.Web.Controllers
{
    public class StatsController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> TopPlayersAsync()
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_DESC"];

            return View("Index", await Plugin.Manager.GetTopStats(0, 15));
        }

        [HttpGet]
        public async Task<IActionResult> GetTopPlayersAsync(int count, int offset)
        {
            return View("_List", await Plugin.Manager.GetTopStats(offset, count));
        }
    }
}
