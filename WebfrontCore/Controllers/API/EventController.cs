using SharedLibraryCore.Dtos;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

using WebfrontCore.Application.API;

namespace WebfrontCore.Controllers.API
{
    public class EventController : APIController
    {
        [HttpGet]
        [Route("event")]
        public ActionResult Index()
        {
            var events = EventAPI.Events;
            var eventsDto = new List<EventInfo>();
            while (events.Count > 0)
                eventsDto.Add(events.Dequeue());
            return Json(eventsDto);
        }
    }
}
