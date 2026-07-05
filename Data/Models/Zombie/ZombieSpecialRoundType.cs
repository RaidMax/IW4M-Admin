namespace Data.Models.Zombie;

/// <summary>
/// Special-round classifications surfaced from GSC <c>GSE;ZW;round_special;&lt;round&gt;;&lt;type&gt;</c>
/// emissions. A non-null value on <see cref="ZombieRoundClientStat.SpecialType"/>
/// indicates the round replaced the regular zombie spawn pool — the static
/// Seconds-Per-Horde formula doesn't apply, and the Round Breakdown UI badges
/// the row.
///
/// Values are stable wire identifiers (do not renumber) — the parser maps GSC
/// string tokens to these via <c>ZombieSpecialRoundTypeExtensions.FromGsc</c>.
/// Mid-round mini-bosses (panzer/brutus/mechz/ghost/sloth) are deliberately
/// NOT listed — those add a small fixed enemy count alongside regular zombies
/// and SPH stays approximately correct.
/// </summary>
public enum ZombieSpecialRoundType
{
    /// <summary>Hellhound rounds — universal across T4/T5/T6/T7.</summary>
    Dog = 1,

    /// <summary>
    /// Space Monkey rounds — T5 Ascension and T7 Ascension (Chronicles).
    /// Shangri-La's GSC initialises the monkey_round flag too but the shipped
    /// game gates it to "never fires" / dead-path.
    /// </summary>
    Monkey = 2,

    /// <summary>Leaper rounds — T6 Die Rise only.</summary>
    Leaper = 3,

    /// <summary>Pentagon Thief rounds — T5 Five.</summary>
    Thief = 4,

    /// <summary>Apothicon Servant ("wasp") rounds — T7 Revelations.</summary>
    Wasp = 5,

    /// <summary>"Spiders from Mars" rounds — T7 Zetsubou No Shima.</summary>
    Spider = 6,

    /// <summary>Three-robot rounds — T7 Origins-style maps.</summary>
    Robot = 7,

    /// <summary>Quad-squad rounds — T7 Kino der Toten (Chronicles).</summary>
    Quad = 8,

    /// <summary>Generic boss rounds — T7 Revelations boss encounter.</summary>
    Boss = 9,

    /// <summary>Easter-egg round — T7 Gorod Krovi.</summary>
    Ee = 10,
}

public static class ZombieSpecialRoundTypeExtensions
{
    /// <summary>
    /// Maps a GSC wire token to its enum value. Returns null on unknown tokens
    /// so a future GSC type doesn't crash the parser before the C# side updates.
    /// </summary>
    public static ZombieSpecialRoundType? FromGsc(string token) => token switch
    {
        "dog"    => ZombieSpecialRoundType.Dog,
        "monkey" => ZombieSpecialRoundType.Monkey,
        "leaper" => ZombieSpecialRoundType.Leaper,
        "thief"  => ZombieSpecialRoundType.Thief,
        "wasp"   => ZombieSpecialRoundType.Wasp,
        "spider" => ZombieSpecialRoundType.Spider,
        "robot"  => ZombieSpecialRoundType.Robot,
        "quad"   => ZombieSpecialRoundType.Quad,
        "boss"   => ZombieSpecialRoundType.Boss,
        "ee"     => ZombieSpecialRoundType.Ee,
        _        => null,
    };

    /// <summary>Inverse of <see cref="FromGsc"/> — used for UI lookups (translation
    /// keys, badge icons) keyed by the canonical lower-case token.</summary>
    public static string ToToken(this ZombieSpecialRoundType type) => type switch
    {
        ZombieSpecialRoundType.Dog    => "dog",
        ZombieSpecialRoundType.Monkey => "monkey",
        ZombieSpecialRoundType.Leaper => "leaper",
        ZombieSpecialRoundType.Thief  => "thief",
        ZombieSpecialRoundType.Wasp   => "wasp",
        ZombieSpecialRoundType.Spider => "spider",
        ZombieSpecialRoundType.Robot  => "robot",
        ZombieSpecialRoundType.Quad   => "quad",
        ZombieSpecialRoundType.Boss   => "boss",
        ZombieSpecialRoundType.Ee     => "ee",
        _ => string.Empty,
    };
}
