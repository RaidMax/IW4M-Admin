using Data.Models;
using SharedLibraryCore.Database.Models;

namespace Integrations.SevenDaysToDie;

public sealed class SevenDaysToDiePlayer : EFClient
{
    public int EntityId { get; init; }
    public int ZombieKills { get; init; }
    public int PlayerDeaths { get; init; }
    public Vector3 Position { get; init; } = new();
    public Vector3 Rotation { get; init; } = new();
    public int Health { get; init; }
}
