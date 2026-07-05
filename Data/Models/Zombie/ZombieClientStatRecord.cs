#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client;

namespace Data.Models.Zombie;

public enum RecordType
{
    Maximum,
    Minimum
}

public class ZombieClientStatRecord : DatedRecord
{
    [Key]
    public int ZombieClientStatRecordId { get; set; }

    [NotMapped] public override long Id => ZombieClientStatRecordId;
    
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    
    public int? ClientId { get; set; }
    [ForeignKey(nameof(ClientId))]
    public virtual EFClient? Client { get; set; }
    
    public long? RoundId { get; set; }
    [ForeignKey(nameof(RoundId))]
    public virtual ZombieRoundClientStat? Round { get; set; }
}
