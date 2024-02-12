using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client;

namespace Data.Models.Zombie;

public enum EventLogType
{
    Default = 0,
    PerformanceCluster = 1,
    DamageTaken = 2,
    Downed = 3,
    Died = 4,
    Revived = 5,
    WasRevived = 6,
    PerkConsumed = 7,
    PowerupGrabbed = 8,
    RoundCompleted = 9,
    JoinedMatch = 10,
    LeftMatch = 11,
    MatchStarted = 12,
    MatchEnded = 13
}

public class ZombieEventLog : DatedRecord
{
    [Key]
    public long ZombieEventLogId { get; set; }

    [NotMapped] public override long Id => ZombieEventLogId;
    
    public EventLogType EventType { get; set; }

    public int? SourceClientId { get; set; }
    [ForeignKey(nameof(SourceClientId))]
    public EFClient SourceClient { get; set; }
    
    public int? AssociatedClientId { get; set; }
    [ForeignKey(nameof(AssociatedClientId))]
    public EFClient AssociatedClient { get; set; }
    
    public double? NumericalValue { get; set; }
    public string TextualValue { get; set; }
    
    public int? MatchId { get; set; }
    [ForeignKey(nameof(MatchId))]
    public ZombieMatch Match { get; set; }
}
