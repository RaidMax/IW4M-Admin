using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Stats.Models;

namespace Data.Models;

public class EFPenaltyIdentifier : AuditFields
{
    [Key]
    public int PenaltyIdentifierId { get; set; }
    
    public int? IPv4Address { get; set; }
    
    [Required]
    public long NetworkId { get; set; }
    
    [Required]
    public int PenaltyId { get; set; }
    
    [ForeignKey(nameof(PenaltyId))]
    public EFPenalty Penalty { get; set; }
}
