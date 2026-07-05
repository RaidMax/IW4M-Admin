using Data.Models.Client;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Fired when a player paid for a Gobble Gum at a BGB machine but did not
/// grab it before the machine cycle ended (walked away / engine auto-return).
/// Cost was deducted upfront and not refunded — this is the loss event.
/// Wire shape: <c>GSE;ZP;{player};gum;leave;{bgbName};{cost}</c>.
/// </summary>
/// <remarks>
/// Ghost-ball cycles (engine couldn't offer a gum because the selected one was
/// already used out for the match) refund the cost and are deliberately not
/// surfaced — they are not a player decision.
/// </remarks>
public class GobbleGumAbandonedGameEvent : ClientGameEvent
{
    public EFClient Buyer => Origin;

    /// <summary>GSC bgb key of the gum that was offered but not taken.</summary>
    public string GumName { get; init; }

    /// <summary>Cost the player forfeited.</summary>
    public int Cost { get; init; }
}
