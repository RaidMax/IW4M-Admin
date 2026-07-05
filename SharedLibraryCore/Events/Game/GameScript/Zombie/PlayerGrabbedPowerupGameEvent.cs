using Data.Models.Client;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class PlayerGrabbedPowerupGameEvent : ClientGameEvent
{
    public EFClient Grabber => Origin;
    public string PowerupName { get; init; }
}
