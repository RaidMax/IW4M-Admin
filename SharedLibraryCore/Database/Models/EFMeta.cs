using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace SharedLibraryCore.Database.Models
{
    /// <summary>
    /// This class encapsulates any meta fields as a simple string 
    /// </summary>
    public class EFMeta : SharedEntity
    {
        [Key]
        public int MetaId { get; set; }
        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;
        [Required]
        public DateTime Updated { get; set; } = DateTime.UtcNow;
        [Required]
        public int ClientId { get; set; }
        [ForeignKey("ClientId")] // this is the client that the meta belongs to
        public virtual EFClient Client { get; set; }
        [Required]
        [MinLength(3)]
        [StringLength(32)]
        [MaxLength(32)]
        public string Key { get; set; }
        [Required]
        public string Value { get; set; }
        public string Extra { get; set; }
    }
}
