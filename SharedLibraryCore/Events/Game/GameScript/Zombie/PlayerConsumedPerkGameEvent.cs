using Data.Models.Client;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class PlayerConsumedPerkGameEvent : ClientGameEvent
{
    public EFClient Consumer => Origin;
    public string PerkName { get; init; }
}
