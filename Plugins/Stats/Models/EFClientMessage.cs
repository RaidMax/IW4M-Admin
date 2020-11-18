using SharedLibraryCore.Database.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFClientMessage : SharedEntity
    {
        [Key]
        public long MessageId { get; set; }
        public long ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual EFClient Client { get; set; }
        public string Message { get; set; }
        public DateTime TimeSent { get; set; }
        public bool SentIngame { get; set; }
    }
}
