using System.Collections.Generic;
using static IW4MAdmin.Plugins.Stats.Cheat.Detection;
using static SharedLibraryCore.Server;

namespace Stats.Config
{
    public class AnticheatConfiguration
    {
        public bool Enable { get; set; }
        public IDictionary<long, DetectionType[]> ServerDetectionTypes { get; set; } = new Dictionary<long, DetectionType[]>();
        public IList<long> IgnoredClientIds { get; set; } = new List<long>();
        public IDictionary<Game, IDictionary<DetectionType, string[]>> IgnoredDetectionSpecification{ get; set; } = new Dictionary<Game, IDictionary<DetectionType, string[]>>
        {
            {
                Game.IW4, new Dictionary<DetectionType, string[]>
                {
                    { DetectionType.Chest, new[] { "m21.+" } },
                    { DetectionType.Recoil, new[] { "ranger.*_mp", "model1887.*_mp", ".+shotgun.*_mp", "turret_minigun_mp" } },
                    { DetectionType.Button, new[] { ".*akimbo.*" } }
                }
            }
        };
    }
}
