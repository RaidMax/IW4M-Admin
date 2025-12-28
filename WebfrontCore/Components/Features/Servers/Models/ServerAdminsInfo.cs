namespace WebfrontCore.Components.Features.Servers.Models;

public class ServerAdminsInfo
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required List<AdminInfo> Admins { get; set; }
}

public class AdminInfo
{
    public required string Name { get; set; }
    public int ClientId { get; set; }
    public required string Level { get; set; }
    public int LevelInt { get; set; }
}
