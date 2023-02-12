using Data.Models;
using Stats.Client.Game;

namespace Stats.Client.Abstractions;

public interface IWeaponNameParser
{
    WeaponInfo Parse(string weaponName, Reference.Game gameName);
}
