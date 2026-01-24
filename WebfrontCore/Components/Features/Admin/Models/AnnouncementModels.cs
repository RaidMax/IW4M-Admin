namespace WebfrontCore.Components.Features.Admin.Models;

/// <summary>
/// DTO for displaying announcement information
/// </summary>
public class AnnouncementInfo
{
    public int AnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsGlobalNotice { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int CreatedByClientId { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public DateTime? UpdatedDateTime { get; set; }
    
    /// <summary>
    /// Whether the announcement is currently within its display date range
    /// </summary>
    public bool IsCurrentlyDisplayable => IsActive && 
        (!StartAt.HasValue || StartAt <= DateTime.UtcNow) &&
        (!EndAt.HasValue || EndAt >= DateTime.UtcNow);
}

/// <summary>
/// Request model for creating a new announcement
/// </summary>
public class CreateAnnouncementRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsGlobalNotice { get; set; }
}

/// <summary>
/// Request model for updating an existing announcement
/// </summary>
public class UpdateAnnouncementRequest : CreateAnnouncementRequest
{
    public int AnnouncementId { get; set; }
}
