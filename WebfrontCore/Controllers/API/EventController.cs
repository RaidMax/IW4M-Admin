using SharedLibraryCore.Dtos;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;


namespace WebfrontCore.Controllers.API
{
    public class EventController : APIController
    {
        [HttpGet]
        [Route("event")]
        public ActionResult Index(bool shouldConsume = true)
        {
            var events = Manager.GetEventApi().GetEvents(shouldConsume);
            return Json(events);
        }
    }
}
