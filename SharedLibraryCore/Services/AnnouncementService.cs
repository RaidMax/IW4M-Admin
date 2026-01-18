using Data.Abstractions;
using Data.Context;
using Data.Models.Misc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Services;

/// <summary>
/// Service for managing MOTD announcements
/// </summary>
public class AnnouncementService(ILogger<AnnouncementService> logger, IDatabaseContextFactory contextFactory)
    : IAnnouncementService
{
    public async Task<EFAnnouncement?> GetActiveAnnouncementAsync(bool globalOnly = false)
    {
        await using var context = contextFactory.CreateContext();
        var now = DateTime.UtcNow;

        var query = context.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (globalOnly)
        {
            query = query.Where(a => a.IsGlobalNotice);
        }

        // Filter by date range
        query = query.Where(a =>
            (!a.StartAt.HasValue || a.StartAt <= now) &&
            (!a.EndAt.HasValue || a.EndAt >= now));

        return await query
            .Include(a => a.CreatedByClient)
            .ThenInclude(c => c.CurrentAlias)
            .OrderByDescending(a => a.CreatedDateTime)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EFAnnouncement>> GetAllAnnouncementsAsync()
    {
        await using var context = contextFactory.CreateContext();
        return await context.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByClient)
            .ThenInclude(c => c.CurrentAlias)
            .OrderByDescending(a => a.CreatedDateTime)
            .ToListAsync();
    }

    public async Task<EFAnnouncement?> GetAnnouncementByIdAsync(int id)
    {
        await using var context = contextFactory.CreateContext();
        return await context.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByClient)
            .ThenInclude(c => c.CurrentAlias)
            .FirstOrDefaultAsync(a => a.AnnouncementId == id);
    }

    public async Task<EFAnnouncement> CreateAnnouncementAsync(EFAnnouncement announcement)
    {
        await using var context = contextFactory.CreateContext();

        if (announcement.IsActive)
        {
            await DeactivateAllAsync(context);
        }

        announcement.CreatedDateTime = DateTime.UtcNow;
        context.Announcements.Add(announcement);
        await context.SaveChangesAsync();

        logger.LogInformation("Created announcement {AnnouncementId}: {Title}",
            announcement.AnnouncementId, announcement.Title);

        NotifyChanged();

        return announcement;
    }

    public async Task<EFAnnouncement> UpdateAnnouncementAsync(EFAnnouncement announcement)
    {
        await using var context = contextFactory.CreateContext();

        var existing = await context.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == announcement.AnnouncementId);

        if (existing == null)
        {
            throw new InvalidOperationException($"Announcement {announcement.AnnouncementId} not found");
        }

        if (announcement.IsActive && !existing.IsActive)
        {
            await DeactivateAllAsync(context);
        }

        existing.Title = announcement.Title;
        existing.Content = announcement.Content;
        existing.StartAt = announcement.StartAt;
        existing.EndAt = announcement.EndAt;
        existing.IsActive = announcement.IsActive;
        existing.IsGlobalNotice = announcement.IsGlobalNotice;
        existing.UpdatedDateTime = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("Updated announcement {AnnouncementId}: {Title}",
            existing.AnnouncementId, existing.Title);

        NotifyChanged();

        return existing;
    }

    public async Task DeleteAnnouncementAsync(int id)
    {
        await using var context = contextFactory.CreateContext();

        var announcement = await context.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == id);

        if (announcement != null)
        {
            context.Announcements.Remove(announcement);
            await context.SaveChangesAsync();

            logger.LogInformation("Deleted announcement {AnnouncementId}: {Title}",
                announcement.AnnouncementId, announcement.Title);
            NotifyChanged();
        }
    }

    public async Task ActivateAnnouncementAsync(int id)
    {
        await using var context = contextFactory.CreateContext();

        // Deactivate all announcements first
        await DeactivateAllAsync(context);

        var announcement = await context.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == id);

        if (announcement != null)
        {
            announcement.IsActive = true;
            announcement.UpdatedDateTime = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogInformation("Activated announcement {AnnouncementId}: {Title}",
                announcement.AnnouncementId, announcement.Title);
            NotifyChanged();
        }
    }

    public async Task DeactivateAnnouncementAsync(int id)
    {
        await using var context = contextFactory.CreateContext();

        var announcement = await context.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == id);

        if (announcement != null)
        {
            announcement.IsActive = false;
            announcement.UpdatedDateTime = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogInformation("Deactivated announcement {AnnouncementId}: {Title}",
                announcement.AnnouncementId, announcement.Title);
            NotifyChanged();
        }
    }

    private static async Task DeactivateAllAsync(DatabaseContext context)
    {
        var activeAnnouncements = await context.Announcements
            .Where(a => a.IsActive)
            .ToListAsync();

        foreach (var announcement in activeAnnouncements)
        {
            announcement.IsActive = false;
            announcement.UpdatedDateTime = DateTime.UtcNow;
        }
    }

    public event Action? OnAnnouncementChanged;

    private void NotifyChanged()
    {
        OnAnnouncementChanged?.Invoke();
    }
}
