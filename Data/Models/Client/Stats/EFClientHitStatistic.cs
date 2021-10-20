using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;
using Stats.Models;

namespace Data.Models.Client.Stats
{
    public class EFClientHitStatistic : AuditFields
    {
        [Key] 
        public int ClientHitStatisticId { get; set; }

        [Required] 
        public int ClientId { get; set; }

        [ForeignKey(nameof(ClientId))]
        public virtual EFClient Client { get; set; }
        
        public long? ServerId { get; set; }
        
        [ForeignKey(nameof(ServerId))]
        public virtual EFServer Server { get; set; }

        public int? HitLocationId { get; set; }

        [ForeignKey(nameof(HitLocationId))] 
        public virtual EFHitLocation HitLocation { get; set; }
        
        public int? MeansOfDeathId { get; set; }
        
        [ForeignKey(nameof(MeansOfDeathId))]
        public virtual EFMeansOfDeath MeansOfDeath { get; set; }

        public int? WeaponId { get; set; }

        [ForeignKey(nameof(WeaponId))]
        public virtual EFWeapon Weapon { get; set; }

        public int? WeaponAttachmentComboId { get; set; }

        [ForeignKey(nameof(WeaponAttachmentComboId))]
        public virtual EFWeaponAttachmentCombo WeaponAttachmentCombo { get; set; }

        /// <summary>
        /// how many hits the player got
        /// </summary>
        public int HitCount { get; set; }

        /// <summary>
        /// how many kills the player got
        /// </summary>
        public int KillCount { get; set; }

        /// <summary>
        /// how much damage the player inflicted
        /// </summary>
        public int DamageInflicted { get; set; }

        /// <summary>
        /// how many hits the player received
        /// </summary>
        public int ReceivedHitCount { get; set; }

        /// <summary>
        /// how many kills the player received
        /// </summary>
        public int DeathCount { get; set; }

        /// <summary>
        /// how much damage the player received
        /// </summary>
        public int DamageReceived { get; set; }

        /// <summary>
        /// how many times the player killed themself 
        /// </summary>
        public int SuicideCount { get; set; }

        /// <summary>
        /// estimation of time spent with the configuration
        /// </summary>
        public int? UsageSeconds { get; set; }
        
        /// <summary>
        /// total in-game score
        /// </summary>
        public int? Score { get; set; }
    }
}
