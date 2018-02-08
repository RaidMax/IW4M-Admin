using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Database.Models
{
    public class EFClient : SharedEntity
    {
        [Key]
        public int ClientId { get; set; }
        [Index(IsUnique = true)]
        public string NetworkId { get; set; }

        [Required]
        public int Connections { get; set; }
        [Required]
        // in seconds 
        public int TotalConnectionTime { get; set; }
        [Required]
        public DateTime FirstConnection { get; set; }
        [Required]
        public DateTime LastConnection { get; set; }
        public bool Masked { get; set; }
        [Required]
        public int AliasLinkId { get; set; }
        [ForeignKey("AliasLinkId")]
        public virtual EFAliasLink AliasLink { get; set; }
        [Required]
        public Objects.Player.Permission Level { get; set; }

        [Required]
        public int CurrentAliasId { get; set; }
        [ForeignKey("CurrentAliasId")]
        public virtual EFAlias CurrentAlias { get; set; }

        [NotMapped]
        public virtual string Name
        {
            get { return CurrentAlias.Name; }
            set { }
        }
        [NotMapped]
        public virtual string IPAddress
        {
            get { return CurrentAlias.IPAddress; }
            set { }
        }

        public virtual ICollection<EFPenalty> ReceivedPenalties { get; set; }
        public virtual ICollection<EFPenalty> AdministeredPenalties { get; set; }

        public EFClient()
        {
            ReceivedPenalties = new List<EFPenalty>();
            AdministeredPenalties = new List<EFPenalty>();
        }
    }
}
