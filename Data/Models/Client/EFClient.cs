using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Zombie;

namespace Data.Models.Client
{
    public class EFClient : SharedEntity
    {
        public enum Permission
        {
            /// <summary>
            /// client has been banned
            /// </summary>
            Banned = -1,
            /// <summary>
            /// default client state upon first connect
            /// </summary>
            User = 0,
            /// <summary>
            /// client has been flagged
            /// </summary>
            Flagged = 1,
            /// <summary>
            /// client is trusted
            /// </summary>
            Trusted = 2,
            /// <summary>
            /// client is a moderator
            /// </summary>
            Moderator = 3,
            /// <summary>
            /// client is an administrator
            /// </summary>
            Administrator = 4,
            /// <summary>
            /// client is a senior administrator
            /// </summary>
            SeniorAdmin = 5,
            /// <summary>
            /// client is a owner
            /// </summary>
            Owner = 6,
            /// <summary>
            /// not used
            /// </summary>
            Creator = 7,
            /// <summary>
            /// reserved for default account
            /// </summary>
            Console = 8
        }

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
        public Reference.Game GameName { get; set; } = Reference.Game.UKN;
        public bool Masked { get; set; }
        [Required]
        public int AliasLinkId { get; set; }
        [ForeignKey("AliasLinkId")]
        public virtual EFAliasLink AliasLink { get; set; }
        [Required]
        public Permission Level { get; set; }

        [Required]
        public int CurrentAliasId { get; set; }
        [ForeignKey("CurrentAliasId")]
        public virtual EFAlias CurrentAlias { get; set; }

        public string Password { get; set; }
        public string PasswordSalt { get; set; }
        // list of meta for the client
        public virtual ICollection<EFMeta> Meta { get; set; }        
        public virtual ICollection<EFPenalty> ReceivedPenalties { get; set; }
        public virtual ICollection<EFPenalty> AdministeredPenalties { get; set; }
        public virtual ICollection<ZombieClientStat> ZombieClientStats { get; set; }
    }
}
