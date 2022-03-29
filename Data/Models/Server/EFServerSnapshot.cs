using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client.Stats.Reference;

namespace Data.Models.Server
{
    public class EFServerSnapshot : SharedEntity
    {
        [Key]
        public long ServerSnapshotId { get; set; }
        
        public DateTime CapturedAt { get; set; }
        
        /// <summary>
        /// Specifies at which time block during a period the snapshot occured
        /// | 1:00      | 1:05      | 1:10      |
        /// | 5 minutes | 5 minutes | 5 minutes |
        /// | 0         | 1         | 2         | 
        /// </summary>
        public int PeriodBlock { get; set; }

        [Required]
        public long ServerId { get; set; }
        
        [ForeignKey(nameof(ServerId))]
        public EFServer Server { get; set; }
        
        public int MapId { get; set; }
        
        [ForeignKey(nameof(MapId))]
        public EFMap Map { get; set; }
        
        public int ClientCount { get; set; }
        
        public bool? ConnectionInterrupted {get;set;}
    }
}
