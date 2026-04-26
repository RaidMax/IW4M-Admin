#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;

namespace Data.Models.Zombie;

public class ZombieMatch : DatedRecord
{
    [Key]
    public int ZombieMatchId { get; set; }

    [NotMapped] public override long Id => ZombieMatchId;
    
    public int? MapId { get; set; }
    [ForeignKey(nameof(MapId))]
    public virtual EFMap? Map { get; set; }
    
    public long? ServerId { get; set; }
    [ForeignKey(nameof(ServerId))]
    public virtual EFServer? Server { get; set; }
    
    public int ClientsCompleted { get; set; }

    /// <summary>
    /// Number of qualifying players (>50% round participation) in this match.
    /// Calculated at match end.
    /// </summary>
    public int? PlayerCount { get; set; }

    /// <summary>
    /// The highest round number reached in this match.
    /// Calculated at match end.
    /// </summary>
    public int HighestRound { get; set; }

    public DateTimeOffset MatchStartDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MatchEndDate { get; set; }

    /// <summary>
    /// Per-map-load identifier emitted by the GSC's <c>sv_iw4m_zm_matchid</c> dvar.
    /// Lets IW4MAdmin re-attach to an existing match row across restarts/reconnects
    /// instead of creating a new orphaned match. Server-scoped via the (ServerId,
    /// GameMatchId) lookup; null for matches predating the GSC dvar deploy.
    /// </summary>
    [MaxLength(64)]
    public string? GameMatchId { get; set; }
}
