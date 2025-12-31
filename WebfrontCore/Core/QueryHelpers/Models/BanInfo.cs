using Data.Models;

namespace WebfrontCore.Core.QueryHelpers.Models;

public class BanInfo
{
    public required string ClientName { get; set; }
    public int ClientId { get; set; }
    public int? IPAddress { get; set; }
    public long NetworkId { get; set; }
    public Reference.Game Game { get; set; }
    public PenaltyInfo? AttachedPenalty { get; set; }
    public IEnumerable<PenaltyInfo> AssociatedPenalties { get; set; } = [];
}

public class PenaltyInfo
{
    public required RelatedClientInfo OffenderInfo { get; set; }
    public required RelatedClientInfo PunisherInfo { get; set; }
    public required string Offense { get; set; }
    public DateTime? DateTime { get; set; }

    public long? TimeStamp =>
        DateTime.HasValue ? new DateTimeOffset(DateTime.Value, TimeSpan.Zero).ToUnixTimeSeconds() : null;
}

public class RelatedClientInfo
{
    public required string ClientName { get; set; }
    public int? ClientId { get; set; }
    public int? IPAddress { get; set; }
    public long? NetworkId { get; set; }
}
