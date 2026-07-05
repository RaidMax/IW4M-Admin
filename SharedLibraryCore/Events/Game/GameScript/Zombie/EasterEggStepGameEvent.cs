namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Match-level progress marker for a single EE step (radio shot, fly-trap link, staff
/// charged, etc.). Distinct from <see cref="EasterEggCompleteGameEvent"/> which fires
/// once at the canonical terminal notify — steps are the granular waypoints leading
/// up to it. No specific player "owns" the step (team milestone), mirroring the
/// canonical event's no-<c>ClientGameEvent</c> base.
///
/// Idempotency lives at the consumer (premium handler dedups by
/// <c>(MatchId, StepKey)</c>) — the GSC is allowed to fire a step once per real
/// in-game trigger, but engine quirks may double-fire and we tolerate it.
/// </summary>
public class EasterEggStepGameEvent : GameEventV2
{
    /// <summary>
    /// Globally-unique step identifier (e.g. <c>t4_vr_radio_1</c>). Format is
    /// <c>&lt;game&gt;_&lt;map_short&gt;_&lt;type&gt;_&lt;index&gt;</c>; the premium plugin's
    /// <c>MapEasterEggConfig</c> determines which keys are valid for which map.
    /// </summary>
    public string StepKey { get; init; } = string.Empty;
}
