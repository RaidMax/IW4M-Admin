using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers.API.Models;

public class TopStatsRequest : PaginationRequest
{
    public string? ServerId { get; set; }

    /// <summary>
    /// Performance-bucket code filter. Stored lower-cased — the DB column
    /// (<c>EFPerformanceBucket.Code</c>) holds the canonical lower-cased form
    /// (the writer in <c>IW4MServer</c> normalises on insert), so any user-
    /// supplied value that retains the original casing (e.g. <c>"Zombies"</c>
    /// from <c>IW4MAdminSettings.json</c>) would otherwise return zero rows
    /// when compared with case-sensitive equality. Empty/whitespace stays null
    /// so callers can distinguish "no filter" from the default bucket.
    /// Mirrors <c>SharedLibraryCore.Helpers.PerformanceBucketCodes.Normalize</c>.
    /// </summary>
    public string? PerformanceBucketCode
    {
        get;
        init => field = string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant();
    }
}
