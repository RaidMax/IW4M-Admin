using SharedLibrary.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Cheat
{
    class DetectionPenaltyResult
    {
        public Penalty.PenaltyType ClientPenalty { get; set; }
        public double RatioAmount { get; set; }
        public IW4Info.HitLocation Bone { get; set; }
        public int KillCount { get; set; }
    }
}
