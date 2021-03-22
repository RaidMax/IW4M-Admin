using Data.Models;
using Stats.Client.Game;

namespace IW4MAdmin.Plugins.Stats.Client.Game
{
    public enum HitType
    {
        Unknown,
        Kill,
        Damage,
        WasKilled,
        WasDamaged,
        Suicide
    }

    public class HitInfo
    {
        public Reference.Game Game { get; set; }
        public int EntityId { get; set; }
        public bool IsVictim { get; set; }
        public HitType HitType { get; set; }
        public int Damage { get; set; }
        public string Location { get; set; }
        public string MeansOfDeath { get; set; }
        public WeaponInfo Weapon { get; set; }
    }
}