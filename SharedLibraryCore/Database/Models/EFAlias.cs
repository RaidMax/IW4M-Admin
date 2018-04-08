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
       // [Index("IX_IPandName", 0, IsUnique = true)]
        //[MaxLength(24)]
        [Required]
        public string Name { get; set; }
       //  [Index("IX_IPandName", 1, IsUnique = true)]
       // [MaxLength(24)]
       [Required]
        public int IPAddress { get; set; }
        [Required]
        public DateTime DateAdded { get; set; }
    }
}
