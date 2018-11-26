using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    public class EFAlias : SharedEntity
    {
        [Key]
        public int AliasId { get; set; }
        [Required]
        public int LinkId { get; set; }
        [ForeignKey("LinkId")]
        public virtual EFAliasLink Link { get; set; }
        [Required]
        [MaxLength(24)]
        public string Name { get; set; }
        [Required]
        public int? IPAddress { get; set; }
        [Required]
        public DateTime DateAdded { get; set; }
    }
}
