using SharedLibrary.Database.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Models
{
    public class EFHitLocationCount : SharedEntity
    {
        [Key]
        public int HitLocationCountId { get; set; }
        [Required]
        public IW4Info.HitLocation Location { get; set; }
        [Required]
        public int HitCount { get; set; }
    }
}
