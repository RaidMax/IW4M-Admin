using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    public partial class EFAlias : SharedEntity
    {
        [Key]
        public int AliasId { get; set; }
        [Required]
        public int LinkId { get; set; }
        [ForeignKey("LinkId")]
        public virtual EFAliasLink Link { get; set; }
        [Required]
        [MaxLength(MAX_NAME_LENGTH)]
        public string Name { get; set; }
        [MaxLength(MAX_NAME_LENGTH)]
        public string SearchableName { get; set; }
        [Required]
        public int? IPAddress { get; set; }
        [Required]
        public DateTime DateAdded { get; set; }

        [NotMapped]
        public const int MAX_NAME_LENGTH = 24;
    }
}
