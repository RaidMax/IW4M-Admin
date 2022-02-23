using System;
using System.ComponentModel.DataAnnotations;
using Data.Abstractions;

namespace Stats.Models
{
    public class AuditFields : IAuditFields
    {
        [Required]
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDateTime { get; set; } 
    }
}
