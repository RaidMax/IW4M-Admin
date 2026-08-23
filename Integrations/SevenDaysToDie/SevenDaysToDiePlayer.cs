namespace Integrations.SevenDaysToDie;

public sealed record SevenDaysToDiePlayer(
    int Slot,
    int EntityId,
    long NetworkId,
    string Name,
    string Address,
    int Ping,
    int Level,
    int ZombieKills,
    int Deaths,
    double PositionX,
    double PositionY,
    double PositionZ,
    double RotationX,
    double RotationY,
    double RotationZ,
    int Health);
