using Data.Models.Client;

namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

public class PlayerRevivedGameEvent : ClientGameEvent
{
    public EFClient Reviver => Origin;
    public EFClient Revived => Target;
}
