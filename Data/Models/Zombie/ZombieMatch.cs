#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;

namespace Data.Models.Zombie;

public class ZombieMatch : DatedRecord
{
    [Key]
    public int ZombieMatchId { get; set; }
    
    public int? MapId { get; set; }
    [ForeignKey(nameof(MapId))]
    public virtual EFMap? Map { get; set; }
    
    public long? ServerId { get; set; }
    [ForeignKey(nameof(ServerId))]
    public virtual EFServer? Server { get; set; }
    
    public int ClientsCompleted { get; set; }
    
    public virtual ICollection<ZombieClientStat>? ClientStats { get; set; } 

    public DateTimeOffset MatchStartDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MatchEndDate { get; set; }
}
