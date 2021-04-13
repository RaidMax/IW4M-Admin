using System.ComponentModel.DataAnnotations;
using Data.Abstractions;
using Stats.Models;

namespace Data.Models.Client.Stats.Reference
{
    public class EFMap : AuditFields, IUniqueId
    {
        [Key]
        public int MapId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public Models.Reference.Game Game { get; set; }

        public long Id => MapId;
        public string Value => Name;
    }
}