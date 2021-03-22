using System.ComponentModel.DataAnnotations;
using Data.Abstractions;
using Stats.Models;

namespace Data.Models.Client.Stats.Reference
{
    public class EFMeansOfDeath: AuditFields, IUniqueId
    {
        [Key]
        public int MeansOfDeathId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public Models.Reference.Game Game { get; set; }

        public long Id => MeansOfDeathId;
        public string Value => Name;
    }
}