namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

/// <summary>
/// Match-level event fired when the map's power state changes — power switch hit,
/// pylon repaired (TranZit), bus power lost, etc. Distinct from <see cref="ZE"/>
/// player-action events because power is a world property, not a player property.
///
/// Attribution is optional. When a player triggers the change (use trigger fired)
/// the wire format includes the player block and <see cref="Origin"/> is populated.
/// When the world flips state (TranZit bus power loss, scripted auto-activation)
/// <see cref="Origin"/> is null and <see cref="Source"/> is <see cref="PowerSource.World"/>.
/// </summary>
public class PowerStateChangeGameEvent : GameEventV2
{
    public PowerState State { get; init; }
    public PowerSource Source { get; init; }
}

public enum PowerState
{
    On = 0,
    Off = 1
}

public enum PowerSource
{
    /// <summary>Game/world flipped power state without a player triggering it.</summary>
    World = 0,
    /// <summary>Player flipped the switch / repaired the pylon. <see cref="GameEventV2.Origin"/> is set.</summary>
    Player = 1
}
