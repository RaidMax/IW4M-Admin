using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Abstractions;

namespace Data.Models.Server
{
    public class EFServer : SharedEntity, IUniqueId
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long ServerId { get; set; }
        [Required]
        public int Port { get; set; }
        public string EndPoint { get; set; }
        public Reference.Game? GameName { get; set; }
        public string HostName { get; set; }
        public bool IsPasswordProtected { get; set; }
        public long Id => ServerId;
        public string Value => EndPoint;
    }
}
