using Data.Models.Client;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Fired when a player grabs a T7 Gobble Gum from a BGB machine (the "take"
/// branch of the machine flow). Wire shape:
/// <c>GSE;ZP;{player};gum;take;{bgbName};{cost}</c>.
/// </summary>
/// <remarks>
/// "Leave" (player walks away without grabbing) has no engine notify, so it is
/// not surfaced. Activated-gum consumption is a separate event —
/// <see cref="GobbleGumActivatedGameEvent"/>.
/// </remarks>
public class GobbleGumTakenGameEvent : ClientGameEvent
{
    public EFClient Buyer => Origin;

    /// <summary>GSC bgb key, e.g. <c>zm_bgb_cache_back</c>.</summary>
    public string GumName { get; init; }

    /// <summary>
    /// Machine cost charged. Default 500 (base) or 3000 (Mega/Ultra rarity tiers
    /// — engine bumps <c>self.current_cost</c> accordingly).
    /// </summary>
    public int Cost { get; init; }
}
