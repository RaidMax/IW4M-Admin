using Data.Models.Client;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Fired when a player activates a T7 Gobble Gum (BGB) of the "activated"
/// limit_type. Non-activated types (time / rounds / event-limited) do not fire
/// the underlying <c>bgb_activation</c> GSC notify and therefore do not surface
/// here. Wire shape: <c>GSE;ZP;{player};gum;activate;{bgbName}</c>.
/// </summary>
public class GobbleGumActivatedGameEvent : ClientGameEvent
{
    public EFClient Activator => Origin;

    /// <summary>
    /// The GSC bgb key, e.g. <c>zm_bgb_perkaholic</c> / <c>zm_bgb_anywhere_but_here</c>.
    /// Not normalised here — downstream callers decide whether to strip the
    /// <c>zm_bgb_</c> prefix for display.
    /// </summary>
    public string GumName { get; init; }
}
