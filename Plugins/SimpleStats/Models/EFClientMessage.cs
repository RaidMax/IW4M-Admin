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
    public class EFClientMessage : SharedEntity
    {
        [Key]
        public long MessageId { get; set; }
        public int ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual EFClient Client { get; set; }
        public string Message { get; set; }
        public DateTime TimeSent { get; set; }
    }
}
