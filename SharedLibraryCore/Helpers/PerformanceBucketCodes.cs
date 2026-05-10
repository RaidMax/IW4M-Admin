namespace SharedLibraryCore.Helpers;

/// <summary>
/// Canonical handling for performance-bucket codes. A null/empty code in
/// <c>IW4MAdminSettings.PerformanceBucketCode</c> represents the implicit
/// "default" bucket — same logical pool as a server explicitly tagged
/// <c>"default"</c>. Centralising the normalisation here avoids the
/// historical inconsistency where seed loops, query filters, and writers
/// each picked a different fallback (<c>"null"</c> vs <c>""</c> vs SQL
/// NULL FK) and silently produced empty leaderboards.
///
/// Lives in SharedLibraryCore so both base Stats and SharedLibraryCore-level
/// helpers (e.g. <see cref="PerformanceBucketClassifier"/>) can share one
/// normaliser instead of redefining it per layer.
/// </summary>
public static class PerformanceBucketCodes
{
    public const string Default = "default";

    /// <summary>True when <paramref name="code"/> represents the default bucket
    /// (null, empty, or the literal "default" in any casing).</summary>
    public static bool IsDefault(string code) =>
        string.IsNullOrEmpty(code) || string.Equals(code, Default, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Lower-cases the code (the DB writer in IW4MServer normalises on insert)
    /// and collapses null/empty to <see cref="Default"/>. All cache keys, DB filter
    /// values, and writer FK lookups must run through this.</summary>
    public static string Normalize(string code) =>
        string.IsNullOrEmpty(code) ? Default : code.ToLowerInvariant();
}
