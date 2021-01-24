using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    /// <summary>
    /// This class encapsulates any meta fields as a simple string 
    /// </summary>
    public class EFMeta : SharedEntity
    {
        public const string ClientTagName = nameof(ClientTagName);
        public const string ClientTag = nameof(ClientTag);

        [Key]
        public int MetaId { get; set; }
        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;
        [Required]
        public DateTime Updated { get; set; } = DateTime.UtcNow;
        public int? ClientId { get; set; }
        // this is the client that the meta could belong to
        [ForeignKey(nameof(ClientId))]
        public virtual EFClient Client { get; set; }
        [Required]
        [MinLength(3)]
        [StringLength(32)]
        [MaxLength(32)]
        public string Key { get; set; }
        [Required]
        public string Value { get; set; }
        public string Extra { get; set; }

        public int? LinkedMetaId { get; set; }
        [ForeignKey(nameof(LinkedMetaId))]
        public virtual EFMeta LinkedMeta { get; set; }
    }
}
