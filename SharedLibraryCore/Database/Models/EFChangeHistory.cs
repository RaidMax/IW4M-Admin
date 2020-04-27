using System;
using System.ComponentModel.DataAnnotations;

namespace SharedLibraryCore.Database.Models
{
    /// <summary>
    /// This class models the change to different entities 
    /// </summary>
    public class EFChangeHistory : SharedEntity
    {
        public enum ChangeType
        {
            Permission,
            Ban,
            Command
        }

        [Key]
        public int ChangeHistoryId { get; set; }
        public int OriginEntityId { get; set; }
        public int TargetEntityId { get; set; }
        public int? ImpersonationEntityId { get; set; }
        public ChangeType TypeOfChange { get; set; }
        public DateTime TimeChanged { get; set; } = DateTime.UtcNow;
        [MaxLength(128)]
        public string Comment { get; set; }
        public string PreviousValue { get; set; }
        public string CurrentValue { get; set; }
    }
}
