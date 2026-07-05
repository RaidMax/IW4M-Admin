namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Match-level event fired exactly once per match when the map's main Easter Egg
/// quest reaches its terminal state (Maxis ending on Origins, Tower of Babble on
/// Tranzit, Fly Trap on Der Riese, etc.). No specific player "owns" the EE — it's
/// a team achievement, so this event has no <see cref="ClientGameEvent"/> base.
///
/// GSC emits exactly once via a guard flag (<c>level.zm_stats_ee_fired</c>) so
/// re-emit on script reload is impossible.
/// </summary>
public class EasterEggCompleteGameEvent : GameEventV2
{
    /// <summary>The map's internal name (e.g. <c>zm_tomb</c> for Origins).</summary>
    public string MapName { get; init; } = string.Empty;
}
