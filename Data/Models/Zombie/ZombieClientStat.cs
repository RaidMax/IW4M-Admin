#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client;

namespace Data.Models.Zombie;

public abstract class ZombieClientStat : DatedRecord
{
    [Key]
    public long ZombieClientStatId { get; set; }
    
    public int? MatchId { get; set; }
    
    [ForeignKey(nameof(MatchId))]
    public virtual ZombieMatch? Match { get; set; }
    
    public int ClientId { get; set; }
    [ForeignKey(nameof(ClientId))] 
    public virtual EFClient? Client { get; set; }

    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int DamageDealt { get; set; }
    public int DamageReceived { get; set; }
    public int Headshots { get; set; }
    public int Melees { get; set; }
    public int Downs { get; set; }
    public int Revives { get; set; }
    public long PointsEarned { get; set; }
    public long PointsSpent { get; set; }
    public int PerksConsumed { get; set; }
    public int PowerupsGrabbed { get; set; }
}
