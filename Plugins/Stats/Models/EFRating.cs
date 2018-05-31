using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFRating : SharedEntity
    {
        [Key]
        public int RatingId { get; set; }
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public EFClient Client { get; set; }
        [Required]
        public double Performance { get; set; }
        [Required]
        public int Ranking { get; set; }
    }
}
