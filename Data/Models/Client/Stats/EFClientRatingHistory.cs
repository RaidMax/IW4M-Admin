using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Client.Stats
{
    public class EFClientRatingHistory : SharedEntity
    {
        [Key]
        public int RatingHistoryId { get; set; }
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual EFClient Client { get; set; }
        public virtual ICollection<EFRating> Ratings { get; set; }
    }
}
