using SharedLibraryCore.Objects;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    class DetectionPenaltyResult
    {
        public Detection.DetectionType Type { get; set; }
        public Penalty.PenaltyType ClientPenalty { get; set; }
        public double Value { get; set; }
        public IW4Info.HitLocation Location { get; set; }
        public int HitCount { get; set; }
    }
}
