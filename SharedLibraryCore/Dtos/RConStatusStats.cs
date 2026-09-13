namespace SharedLibraryCore.Dtos;

/// <summary>
///     Live kill/death counters reported by a server status response.
///     These values are display-only and are not persisted as stat events.
/// </summary>
public sealed record RConStatusStats(int Kills, int Deaths);
