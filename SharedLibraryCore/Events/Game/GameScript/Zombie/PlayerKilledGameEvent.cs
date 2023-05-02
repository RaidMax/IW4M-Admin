namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class PlayerKilledGameEvent : PlayerDamageGameEvent
{
    public PlayerKilledGameEvent()
    {
        RequiredEntity = EventRequiredEntity.Target;
    }
}
