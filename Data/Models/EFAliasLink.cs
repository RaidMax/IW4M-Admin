using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class EFAliasLink : SharedEntity
    {
        [Key]
        public int AliasLinkId { get; set; }
        public virtual ICollection<EFAlias> Children { get; set; }
        public virtual ICollection<EFPenalty> ReceivedPenalties { get; set; }

        public EFAliasLink()
        {
            Children = new List<EFAlias>();
            ReceivedPenalties = new List<EFPenalty>();
        }
    }
}
