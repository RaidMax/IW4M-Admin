using Data.Models;

namespace SharedLibraryCore.Dtos;

/// <summary>
///     Extended pagination request with audit-specific filter parameters
/// </summary>
public class AuditFilterRequest : PaginationRequest
{
    /// <summary>
    ///     Filter by action types (Permission, Command). Empty list returns all.
    /// </summary>
    public List<EFChangeHistory.ChangeType> ActionTypes { get; set; } = [];

    /// <summary>
    ///     Filter by admin (origin) client ID
    /// </summary>
    public int? OriginId { get; set; }

    /// <summary>
    ///     Filter by target client ID
    /// </summary>
    public int? TargetId { get; set; }

    /// <summary>
    ///     Text search query for comment/data field
    /// </summary>
    public string SearchQuery { get; set; }
}
