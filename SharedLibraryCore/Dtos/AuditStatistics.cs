using Data.Models;

namespace SharedLibraryCore.Dtos;

/// <summary>
///     Statistics data for audit log dashboard
/// </summary>
public class AuditStatistics
{
    /// <summary>
    ///     Total count of audit entries (respecting current filters)
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    ///     Count of entries grouped by action type
    /// </summary>
    public Dictionary<EFChangeHistory.ChangeType, int> CountByActionType { get; set; } = new();

    /// <summary>
    ///     Top active admins with their action counts
    /// </summary>
    public List<AdminActivityInfo> MostActiveAdmins { get; set; } = [];
}

/// <summary>
///     Admin activity summary for statistics
/// </summary>
public class AdminActivityInfo
{
    public int ClientId { get; set; }
    public string Name { get; set; }
    public int ActionCount { get; set; }
}
