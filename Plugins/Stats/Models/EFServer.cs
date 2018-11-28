
using SharedLibraryCore.Database.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFServer : SharedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long ServerId { get; set; }
        [Required]
        public int Port { get; set; }
        public string EndPoint { get; set; }
    }
}
