using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Database.Models
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
        public string Name { get; set; }
        [Required]
        public string IP { get; set; }
        [Required]
        public DateTime DateAdded { get; set; }
    }
}
