using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Events.Game;

public class ClientDamageEvent : ClientGameEvent
{
    public EFClient Attacker => Origin;

    public void UpdateAttacker(EFClient client)
    {
        Origin = client;
    }

    public EFClient Victim => Target;
    
    public string AttackerClientName => ClientName;
    public string AttackerNetworkId => ClientNetworkId;
    public int AttackerClientSlotNumber => ClientSlotNumber;
    public string AttackerTeamName { get; init; }

    public string VictimClientName { get; init; }
    public string VictimNetworkId { get; init; }
    public int VictimClientSlotNumber { get; init; }
    public string VictimTeamName { get; init; }
    
    public string WeaponName { get; init; }
    public int Damage { get; init; }
    public string MeansOfDeath { get; init; }
    public string HitLocation { get; init; }
}
