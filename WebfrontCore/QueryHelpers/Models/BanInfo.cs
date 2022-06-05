using System;
using System.Collections.Generic;

namespace WebfrontCore.QueryHelpers.Models;

public class BanInfo
{
    public string ClientName { get; set; }
    public int ClientId { get; set; }
    public int? IPAddress { get; set; }
    public long NetworkId { get; set; }
    public PenaltyInfo AttachedPenalty { get; set; }
    public IEnumerable<PenaltyInfo> AssociatedPenalties { get; set; }
}

public class PenaltyInfo
{
    public RelatedClientInfo OffenderInfo { get; set; }
    public RelatedClientInfo PunisherInfo { get; set; }
    public string Offense { get; set; }
    public DateTime? DateTime { get; set; }

    public long? TimeStamp =>
        DateTime.HasValue ? new DateTimeOffset(DateTime.Value, TimeSpan.Zero).ToUnixTimeSeconds() : null;
}

public class RelatedClientInfo
{
    public string ClientName { get; set; }
    public int? ClientId { get; set; }
    public int? IPAddress { get; set; }
    public long? NetworkId { get; set; }
}
