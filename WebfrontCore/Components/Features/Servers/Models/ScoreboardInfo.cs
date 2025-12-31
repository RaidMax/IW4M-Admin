using Data.Models;
using SharedLibraryCore.Database.Models;

namespace WebfrontCore.Components.Features.Servers.Models;

public class ScoreboardInfo
{
    public required string ServerName { get; set; }
    public required string ServerId { get; set; }
    public Reference.Game GameCode { get; set; }
    public required string MapName { get; set; }
    public required string OrderByKey { get; set; }
    public bool ShouldOrderDescending { get; set; }
    public List<ClientScoreboardInfo> ClientInfo { get; set; } = [];
}

public class ClientScoreboardInfo
{
    public required string ClientName { get; set; }
    public long ClientId { get; set; }
    public int Score { get; set; }
    public int Ping { get; set; }
    public int? Kills { get; set; }
    public int? Deaths { get; set; }
    public double? ScorePerMinute { get; set; }
    public double? Kdr { get; set; }
    public double? ZScore { get; set; }
    public EFClient.TeamType Team { get; set; }
    public Data.Models.Client.EFClient.Permission Level { get; set; }
}
