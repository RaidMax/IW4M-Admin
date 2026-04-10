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
}
