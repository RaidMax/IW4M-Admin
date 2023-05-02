namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class ZombieKilledGameEvent : ZombieDamageGameEvent
{
    public ZombieKilledGameEvent()
    {
        RequiredEntity = EventRequiredEntity.Origin;
    }
}
