using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Client.Stats;

public class EFClientStatTagValue : DatedRecord
{
    [Key]
    public long ZombieClientStatTagValueId { get; set; }
    
    public int? StatValue { get; set; }
    
    [Required]
    public int StatTagId { get; set; }
    
    [ForeignKey(nameof(StatTagId))]
    public EFClientStatTag StatTag { get; set; }
    
    public int ClientId { get; set; }
    
    [ForeignKey(nameof(ClientId))]
    public EFClient Client { get; set; }
}
