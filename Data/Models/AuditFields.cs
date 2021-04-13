using System;
using System.ComponentModel.DataAnnotations;

namespace Stats.Models
{
    public class AuditFields
    {
        [Required]
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDateTime { get; set; } 
    }
}