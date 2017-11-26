using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SharedLibrary.Database.Models
{
    public class EFAliasLink : SharedEntity
    {
        [Key]
        public int AliasLinkId { get; set; }
        public virtual ICollection<EFAlias> Children { get; set; }

        public EFAliasLink()
        {
            Children = new List<EFAlias>();
        }
    }
}
