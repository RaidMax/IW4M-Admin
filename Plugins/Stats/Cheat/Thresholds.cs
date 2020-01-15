using Stats.Config;
using System;
using static SharedLibraryCore.Database.Models.EFPenalty;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    public static class DistributionHelper
    {
        public static double CalculateMaxValue(this DistributionConfiguration config, PenaltyType penaltyType, int sampleSize)
        {
            switch (config.Type)
            {
                case DistributionConfiguration.DistributionType.Normal:
                    break;
                case DistributionConfiguration.DistributionType.LogNormal:
                    double deviationNumber = penaltyType == PenaltyType.Flag ? 3.0 : 4.0;
                    double marginOfError = 1.644 / (config.StandardDeviation / Math.Sqrt(sampleSize));
                    double maxValue = (config.StandardDeviation * deviationNumber) + marginOfError;
                    return maxValue;
            }

            return double.MaxValue;
        }
    }

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
        public const int LowSampleMinKillsRecoil = 5;
        public const double SnapFlagValue = 7.6;
        public const double SnapBanValue = 11.7;

        public const double MaxStrainBan = 0.9;

        private const double _offsetMeanLog = -2.3243889;
        private const double _offsetSdLog = 0.5851351;

        public static double MaxOffset(int sampleSize) => Math.Exp(Math.Max(_offsetMeanLog + (_offsetMeanLog / Math.Sqrt(sampleSize)), _offsetMeanLog - (_offsetMeanLog / Math.Sqrt(sampleSize))) + 4 * (_offsetSdLog));
        public const double MaxStrainFlag = 0.36;

        public static double GetMarginOfError(int numberOfHits) => 1.6455 / Math.Sqrt(numberOfHits);

        public static double Lerp(double v1, double v2, double amount)
        {
            return v1 + (v2 - v1) * amount;
        }
    }
}
