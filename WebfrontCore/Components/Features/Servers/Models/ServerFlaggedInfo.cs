namespace WebfrontCore.Components.Features.Servers.Models;

public class ServerFlaggedInfo
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required List<FlaggedClientInfo> FlaggedClients { get; set; }
}

public class FlaggedClientInfo
{
    public required string Name { get; set; }
    public int ClientId { get; set; }
    public required string Reason { get; set; }
    public DateTime FlaggedOn { get; set; }
}
