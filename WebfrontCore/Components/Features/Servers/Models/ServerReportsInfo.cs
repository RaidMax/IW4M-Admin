using Data.Models;

namespace WebfrontCore.Components.Features.Servers.Models;

public class ServerReportsInfo
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public Reference.Game Game { get; set; }
    public required List<ReportInfo> Reports { get; set; }
}

public class ReportInfo
{
    public required ReportEntityInfo Target { get; set; }
    public required ReportEntityInfo Origin { get; set; }
    public required string Reason { get; set; }
    public DateTime ReportedOn { get; set; }
}

public class ReportEntityInfo
{
    public required string Name { get; set; }
    public int ClientId { get; set; }
}
