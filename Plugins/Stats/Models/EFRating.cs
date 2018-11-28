using SharedLibraryCore.Database.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFRating : SharedEntity
    {
        [Key]
        public int RatingId { get; set; }
        public int RatingHistoryId { get; set; }
        [ForeignKey("RatingHistoryId")]
        public virtual EFClientRatingHistory RatingHistory { get; set; }
        // if null, indicates that the rating is an average rating
        public long? ServerId { get; set; }
       // [ForeignKey("ServerId")] can't make this nullable if this annotation is set
        public virtual EFServer Server { get; set; }
        [Required]
        public double Performance { get; set; }
        [Required]
        public int Ranking { get; set; }
        [Required]
        // indicates if the rating is the latest
        public bool Newest { get; set; }
        [Required]
        public int ActivityAmount { get; set; }
        [Required]
        public DateTime When { get; set; } = DateTime.UtcNow;
    }
}
