using System.ComponentModel.DataAnnotations;

namespace Data.Models.Client.Stats;

public class EFClientStatTag : DatedRecord
{
    [Key]
    public int ZombieStatTagId { get; set; }
    
    [MaxLength(128)]
    public string TagName { get; set; }
}
