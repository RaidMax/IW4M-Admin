namespace IW4MAdmin.Plugins.Stats
{
    public class IW4Info
    {
        public enum Team
        {
            None,
            Spectator,
            Allies,
            Axis
        }

        public enum MeansOfDeath
        {
            NONE,
            MOD_UNKNOWN,
            MOD_PISTOL_BULLET,
            MOD_RIFLE_BULLET,
            MOD_GRENADE,
            MOD_GRENADE_SPLASH,
            MOD_PROJECTILE,
            MOD_PROJECTILE_SPLASH,
            MOD_MELEE,
            MOD_BAYONET,
            MOD_HEAD_SHOT,
            MOD_CRUSH,
            MOD_TELEFRAG,
            MOD_FALLING,
            MOD_SUICIDE,
            MOD_TRIGGER_HURT,
            MOD_EXPLOSIVE,
            MOD_IMPACT,
            MOD_BURNED,
            MOD_HIT_BY_OBJECT,
            MOD_DROWN,
            MOD_GAS,
            MOD_NUM,
            MOD_EXPLOSIVE_BULLET
        }

        public enum HitLocation
        {
            none,
            helmet,
            head,
            neck,
            torso_upper,
            torso_lower,
            right_arm_upper,
            left_arm_upper,
            right_arm_lower,
            left_arm_lower,
            right_hand,
            left_hand,
            right_leg_upper,
            left_leg_upper,
            right_leg_lower,
            left_leg_lower,
            right_foot,
            left_foot,
            gun,
            shield
        }
    }
}
