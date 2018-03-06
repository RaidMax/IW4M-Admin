using Microsoft.AspNetCore.Mvc;
using SharedLibrary;
using SharedLibrary.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.ViewComponents;

namespace WebfrontCore.Controllers
{
    public class PenaltyController : BaseController
    {
        public IActionResult List()
        {
            ViewBag.Title = "Penalty List";
            return View();
        }

        public async Task<IActionResult> ListAsync(int offset = 0)
        {
            return View("_List", offset);
        }
    }
}
