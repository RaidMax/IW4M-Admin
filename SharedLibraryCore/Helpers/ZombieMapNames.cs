namespace SharedLibraryCore.Helpers;

/// <summary>
/// Maps internal zombie map code names to display-friendly names.
/// Covers World at War (T4), Black Ops (T5), and Black Ops II (T6).
/// </summary>
public static class ZombieMapNames
{
    private static readonly Dictionary<string, string> MapLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        // World at War (T4)
        { "nazi_zombie_prototype", "Nacht der Untoten" },
        { "nazi_zombie_asylum", "Verrückt" },
        { "nazi_zombie_factory", "Der Riese" },
        { "nazi_zombie_sumpf", "Shi No Numa" },

        // Black Ops (T5)
        { "zombie_theater", "Kino der Toten" },
        { "zombie_pentagon", "Five" },
        { "zombie_cosmodrome", "Ascension" },
        { "zombie_coast", "Call of the Dead" },
        { "zombie_temple", "Shangri-La" },
        { "zombie_moon", "Moon" },
        { "zombietron", "Dead Ops Arcade" },

        // Black Ops - WaW Remakes
        { "zombie_cod5_prototype", "Nacht der Untoten" },
        { "zombie_cod5_asylum", "Verrückt" },
        { "zombie_cod5_sumpf", "Shi No Numa" },
        { "zombie_cod5_factory", "Der Riese" },

        // Black Ops II (T6)
        { "zm_transit", "TranZit" },
        { "zm_nuked", "Nuketown Zombies" },
        { "zm_highrise", "Die Rise" },
        { "zm_buried", "Buried" },
        { "zm_prison", "Mob of the Dead" },
        { "zm_tomb", "Origins" }
    };

    /// <summary>
    /// Returns the display name for a zombie map code name.
    /// Falls back to the original name with underscores replaced by spaces if not found.
    /// </summary>
    public static string GetDisplayName(string? codeName)
    {
        if (string.IsNullOrEmpty(codeName)) return "Unknown";
        return MapLookup.TryGetValue(codeName, out var displayName)
            ? displayName
            : codeName.Replace("_", " ");
    }
}
