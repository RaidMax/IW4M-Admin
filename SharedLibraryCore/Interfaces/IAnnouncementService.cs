using Data.Models.Misc;

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Service for managing MOTD announcements
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// Gets the currently active announcement that should be displayed
    /// </summary>
    /// <param name="globalOnly">If true, only return global announcements; if false, return any active announcement</param>
    /// <returns>The active announcement or null if none</returns>
    Task<EFAnnouncement?> GetActiveAnnouncementAsync(bool globalOnly = false);
    
    /// <summary>
    /// Gets all announcements ordered by creation date descending
    /// </summary>
    Task<IEnumerable<EFAnnouncement>> GetAllAnnouncementsAsync();
    
    /// <summary>
    /// Gets an announcement by its ID
    /// </summary>
    Task<EFAnnouncement?> GetAnnouncementByIdAsync(int id);
    
    /// <summary>
    /// Creates a new announcement
    /// </summary>
    Task<EFAnnouncement> CreateAnnouncementAsync(EFAnnouncement announcement);
    
    /// <summary>
    /// Updates an existing announcement
    /// </summary>
    Task<EFAnnouncement> UpdateAnnouncementAsync(EFAnnouncement announcement);
    
    /// <summary>
    /// Deletes an announcement
    /// </summary>
    Task DeleteAnnouncementAsync(int id);
    
    /// <summary>
    /// Sets an announcement as the active one (deactivates all others)
    /// </summary>
    Task ActivateAnnouncementAsync(int id);
    
    /// <summary>
    /// Deactivates an announcement
    /// </summary>
    Task DeactivateAnnouncementAsync(int id);
    
    /// <summary>
    /// Event raised when an announcement is created, updated, or deleted
    /// </summary>
    event Action OnAnnouncementChanged;
}
