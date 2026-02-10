using System;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Server;

/// <summary>
/// Game-level daily statistics (e.g. aggregated playtime). Per-server stats use <see cref="EFServerStatistics"/>.
/// </summary>
public class EFGameStatistic : SharedEntity
{
    [Key]
    public long Id { get; set; }
    /// <summary>Matches <see cref="Reference.Game"/>; stored as int for EF.</summary>
    public int? GameName { get; set; }
    public DateTime Date { get; set; }
    public long PlayTimeMinutes { get; set; }
    public int UniqueClientCount { get; set; }
    public int ConnectionCount { get; set; }
}
