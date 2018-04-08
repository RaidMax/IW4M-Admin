using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SharedLibraryCore.Database.Models
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
