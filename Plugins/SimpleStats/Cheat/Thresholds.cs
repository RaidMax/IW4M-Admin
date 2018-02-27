using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Cheat
{
    class Thresholds
    {
        private const double Deviations = 3.33;

        public const double HeadshotRatioThresholdLowSample = HeadshotRatioStandardDeviationLowSample * Deviations + HeadshotRatioMean;
        public const double HeadshotRatioThresholdHighSample = HeadshotRatioStandardDeviationHighSample * Deviations + HeadshotRatioMean;
        public const double HeadshotRatioStandardDeviationLowSample = 0.1769994181;
        public const double HeadshotRatioStandardDeviationHighSample = 0.03924263235;
        //public const double HeadshotRatioMean = 0.09587712258;
        public const double HeadshotRatioMean = 0.222;

        public const double BoneRatioThresholdLowSample = BoneRatioStandardDeviationLowSample * Deviations + BoneRatioMean;
        public const double BoneRatioThresholdHighSample = BoneRatioStandardDeviationHighSample * Deviations + BoneRatioMean;
        public const double BoneRatioStandardDeviationLowSample = 0.1324612879;
        public const double BoneRatioStandardDeviationHighSample = 0.0515753935;
        public const double BoneRatioMean = 0.3982907516;

        public const int LowSampleMinKills = 15;
        public const int HighSampleMinKills = 100;
        public const double KillTimeThreshold = 0.2;

        public static double GetMarginOfError(int numKills) => 1.645 /(2 * Math.Sqrt(numKills));

        public static double Lerp(double v1, double v2, double amount)
        {
            return v1 + (v2 - v1) * amount;
        }
    }
}
