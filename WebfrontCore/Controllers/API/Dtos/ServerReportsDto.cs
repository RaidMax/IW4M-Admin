using System;
using System.Collections.Generic;

namespace WebfrontCore.Controllers.API.Dtos
{
    public class ServerReportsDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<ReportDto> Reports { get; set; }
    }

    public class ReportDto
    {
        public EntityDto Target { get; set; }
        public EntityDto Origin { get; set; }
        public string Reason { get; set; }
        public DateTime ReportedOn { get; set; }
    }

    public class EntityDto
    {
        public string Name { get; set; }
        public int ClientId { get; set; }
    }
}
