using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace IW4MAdmin.Plugins.Stats.Models
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
