using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    public class EFClient : SharedEntity
    {
        [Key]
        public int ClientId { get; set; }
        public long NetworkId { get; set; }
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

        public string Password { get; set; }
        public string PasswordSalt { get; set; }
        // list of meta for the client
        public virtual ICollection<EFMeta> Meta { get; set; } 

        [NotMapped]
        public virtual string Name
        {
            get { return CurrentAlias.Name; }
            set { }
        }
        [NotMapped]
        public virtual int IPAddress
        {
            get { return CurrentAlias.IPAddress; }
            set { }
        }

        [NotMapped]
        public string IPAddressString => new System.Net.IPAddress(BitConverter.GetBytes(IPAddress)).ToString();
        [NotMapped]
        public virtual IDictionary<int, long> LinkedAccounts { get; set; }

        public virtual ICollection<EFPenalty> ReceivedPenalties { get; set; }
        public virtual ICollection<EFPenalty> AdministeredPenalties { get; set; }

        public EFClient()
        {
            ReceivedPenalties = new List<EFPenalty>();
            AdministeredPenalties = new List<EFPenalty>();
        }
    }
}
