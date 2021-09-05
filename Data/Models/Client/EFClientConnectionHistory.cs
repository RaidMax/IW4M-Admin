using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Server;
using Stats.Models;

namespace Data.Models.Client
{
    public class EFClientConnectionHistory : AuditFields
    {
        [Key] 
        public long ClientConnectionId { get; set; }

        public int ClientId { get; set; }
        
        [ForeignKey(nameof(ClientId))]
        public EFClient Client { get;set; }
        
        public long ServerId { get; set; }
        
        [ForeignKey(nameof(ServerId))]
        public EFServer Server { get;set; }
        
        public Reference.ConnectionType ConnectionType { get; set; }
    }
}