using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var eventsDto = new List<SharedLibrary.Dtos.EventInfo>();
            while (events.Count > 0)
                eventsDto.Add(events.Dequeue());
            return Json(eventsDto);
        }
    }
}
