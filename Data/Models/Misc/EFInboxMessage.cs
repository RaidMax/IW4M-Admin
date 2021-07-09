using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client;
using Data.Models.Server;
using Stats.Models;

namespace Data.Models.Misc
{
    public class EFInboxMessage : AuditFields
    {
        [Key]
        public int InboxMessageId { get; set; }
        
        [Required]
        public int SourceClientId { get; set; }
        
        [ForeignKey(nameof(SourceClientId))]
        public EFClient SourceClient { get; set; }
        
        [Required]
        public int DestinationClientId { get; set; }
        
        [ForeignKey(nameof(DestinationClientId))]
        public EFClient DestinationClient { get; set; }
        
        public long? ServerId { get; set; }
        
        [ForeignKey(nameof(ServerId))]
        public EFServer Server { get; set; }

        public string Message { get; set; }
        
        public bool IsDelivered { get; set; }
    }
}