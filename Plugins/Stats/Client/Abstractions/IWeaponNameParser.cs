using SharedLibraryCore;
using Stats.Client.Game;

namespace Stats.Client.Abstractions
{
    public interface IWeaponNameParser
    {
        WeaponInfo Parse(string weaponName, Server.Game gameName);
    }
}
