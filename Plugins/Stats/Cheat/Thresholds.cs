using System;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    class Thresholds
    {
        public static double HeadshotRatioThresholdLowSample(double deviations) => HeadshotRatioStandardDeviationLowSample * deviations + HeadshotRatioMean;
        public static double HeadshotRatioThresholdHighSample(double deviations) => HeadshotRatioStandardDeviationHighSample * deviations + HeadshotRatioMean;
        public const double HeadshotRatioStandardDeviationLowSample = 0.1769994181;
        public const double HeadshotRatioStandardDeviationHighSample = 0.03924263235;
        public const double HeadshotRatioMean = 0.222;

        public static double BoneRatioThresholdLowSample(double deviations) => BoneRatioStandardDeviationLowSample * deviations + BoneRatioMean;
        public static double BoneRatioThresholdHighSample(double deviations) => BoneRatioStandardDeviationHighSample * deviations + BoneRatioMean;
        public const double BoneRatioStandardDeviationLowSample = 0.1324612879;
        public const double BoneRatioStandardDeviationHighSample = 0.0515753935;
        public const double BoneRatioMean = 0.4593110238;

        public static double ChestAbdomenRatioThresholdLowSample(double deviations) => ChestAbdomenStandardDeviationLowSample * deviations + ChestAbdomenRatioMean;
        public static double ChestAbdomenRatioThresholdHighSample(double deviations) => ChestAbdomenStandardDeviationHighSample * deviations + ChestAbdomenRatioMean;
        public const double ChestAbdomenStandardDeviationLowSample = 0.2859234644;
        public const double ChestAbdomenStandardDeviationHighSample = 0.2195212861;
        public const double ChestAbdomenRatioMean = 0.4435;

        public const int LowSampleMinKills = 15;
        public const int MediumSampleMinKills = 30;
        public const int HighSampleMinKills = 100;
        public const double KillTimeThreshold = 0.2;

        public const double MaxStrainBan = 0.9;

        public static double MaxOffset(int sampleSize) => Math.Exp(Math.Max(-3.07 + (-3.07 / Math.Sqrt(sampleSize)), -3.07 - (-3.07 / Math.Sqrt(sampleSize))) + 4 * (0.869));
        public const double MaxStrainFlag = 0.36;

        public static double GetMarginOfError(int numKills) => 1.6455 / Math.Sqrt(numKills);

        public static double Lerp(double v1, double v2, double amount)
        {
            return v1 + (v2 - v1) * amount;
        }
    }
}
