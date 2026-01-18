using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client;
using Stats.Models;

namespace Data.Models.Misc;

/// <summary>
/// Represents a Message of the Day announcement for the webfront
/// </summary>
public class EFAnnouncement : AuditFields
{
    [Key]
    public int AnnouncementId { get; set; }
    
    /// <summary>
    /// Title/header of the announcement
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Main content/body of the announcement
    /// </summary>
    [Required]
    [MaxLength(4096)]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// When the announcement should start displaying (null = immediately)
    /// </summary>
    public DateTime? StartAt { get; set; }
    
    /// <summary>
    /// When the announcement should stop displaying (null = indefinite)
    /// </summary>
    public DateTime? EndAt { get; set; }
    
    /// <summary>
    /// Whether this is the currently active announcement. Only one announcement can be active at a time.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Whether this announcement displays on every page (true) or only the home/server overview page (false)
    /// </summary>
    public bool IsGlobalNotice { get; set; }
    
    /// <summary>
    /// Client ID of the user who created this announcement
    /// </summary>
    public int CreatedByClientId { get; set; }
    
    [ForeignKey(nameof(CreatedByClientId))]
    public virtual EFClient? CreatedByClient { get; set; }
}
